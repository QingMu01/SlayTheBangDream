using System.Diagnostics;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Logging;

namespace BangDreamInSpire.Scripts.Diagnostics;

/// <summary>
/// BDS-Perf 的全局时间线与事件记录：mod 初始化埋点、游戏日志捕获、资源预加载会话、同步加载观测。
/// 所有记录都只存在内存里（有上限），写报告时才落盘；这里的方法必须尽量便宜，且绝不抛异常。
/// </summary>
internal static class PerfTimeline
{
    private const int MaxMarkCount = 512;
    private const int MaxLogLines = 3000;
    private const int MaxSyncLoadRecords = 48;
    private const int MaxSessionRecords = 64;
    private const string ModAssetPrefixCore = "res://BangDreamInSpire/";
    private const string ModAssetPrefixCrychic = "res://ItsCrychic/";

    private static readonly object Gate = new();

    // 这些集合只在 Start() 里创建：未启用时连类型初始化都不产生分配。
    private static List<PerfMark> Marks = null!;
    private static Queue<PerfLogLine> RecentLogs = null!;
    private static List<SyncLoadRecord> SyncLoads = null!;
    private static List<PreloadSessionRecord> Sessions = null!;
    private static Dictionary<AssetLoadingSession, PreloadSessionRecord> SessionMap = null!;
    private static Dictionary<string, int> InjectedAssets = null!;

    private static long _engineOffsetMsec = -1;
    private static long _startMsec = -1;
    private static int _state;
    private static string _phase = "startup";
    private static string? _activePreload;
    private static long _mainMenuAtMs = -1;

    // 日志里的「Asset not cached」与补丁观测到的同步加载分开计数，避免重复统计。
    private static long _notCachedCount;
    private static long _notCachedModCount;
    private static long _syncLoadCount;
    private static long _syncLoadModCount;
    private static long _syncLoadTotalUsec;
    private static long _syncLoadMaxUsec;
    private static long _syncFallbackCount;
    private static int _warnCount;
    private static int _errorCount;
    private static int _registeredCards;
    private static int _registeredPowers;
    private static int _registeredRelics;
    private static int _registeredCharacters;
    private static List<string> ModWarnings = null!;

    /// <summary>时间线是否已启动（未启用时为 false，所有写入操作直接短路）。</summary>
    private static bool IsActive => Volatile.Read(ref _state) == 2;

    /// <summary>mod 开始初始化时引擎已运行的毫秒数，用于和 <c>[Startup]</c> 日志里的绝对 tick 对齐。</summary>
    public static long EngineOffsetMsec
    {
        get
        {
            lock (Gate)
            {
                return _engineOffsetMsec;
            }
        }
    }

    public static long ElapsedMs
    {
        get
        {
            long start = Volatile.Read(ref _startMsec);
            if (start < 0)
            {
                return 0;
            }

            return (long)Time.GetTicksMsec() - start;
        }
    }

    public static string CurrentPhase => Volatile.Read(ref _phase) ?? "startup";

    public static string? ActivePreload => Volatile.Read(ref _activePreload);

    public static long MainMenuAtMs => Volatile.Read(ref _mainMenuAtMs);

    /// <summary>在 mod 初始化最开头调用一次，建立相对时间零点。</summary>
    public static void Start()
    {
        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
        {
            return;
        }

        long now;
        try
        {
            now = (long)Time.GetTicksMsec();
        }
        catch
        {
            now = 0;
        }

        Marks = new List<PerfMark>(MaxMarkCount);
        RecentLogs = new Queue<PerfLogLine>();
        SyncLoads = new List<SyncLoadRecord>(MaxSyncLoadRecords);
        Sessions = new List<PreloadSessionRecord>();
        SessionMap = new Dictionary<AssetLoadingSession, PreloadSessionRecord>();
        InjectedAssets = new Dictionary<string, int>();
        ModWarnings = new List<string>(32);

        Volatile.Write(ref _engineOffsetMsec, now);
        Volatile.Write(ref _startMsec, now);
        Volatile.Write(ref _state, 2);
        Mark("mod:init:begin");
    }

    public static void Mark(string label, string? detail = null)
    {
        if (!IsActive)
        {
            return;
        }

        try
        {
            lock (Gate)
            {
                AddMarkLocked(new PerfMark(ElapsedMs, label, 0, detail));
            }
        }
        catch
        {
            // 诊断代码绝不影响游戏。
        }
    }

    /// <summary>执行一段同步代码并记录耗时（毫秒）。</summary>
    public static long Measure(string label, Action action)
    {
        if (!IsActive)
        {
            action();
            return 0;
        }

        long start = Stopwatch.GetTimestamp();
        try
        {
            action();
        }
        catch
        {
            MarkMeasured(label, ElapsedMsSince(start), "failed");
            throw;
        }

        long elapsedMs = ElapsedMsSince(start);
        MarkMeasured(label, elapsedMs, null);
        return elapsedMs;
    }

    public static void SetPhase(string phase)
    {
        Volatile.Write(ref _phase, phase);
    }

    public static void OnMainMenuReady()
    {
        lock (Gate)
        {
            long at = ElapsedMs;
            Volatile.Write(ref _mainMenuAtMs, at);
            Volatile.Write(ref _phase, "main-menu");
            AddMarkLocked(new PerfMark(at, "event:main-menu-ready", 0, null));
        }
    }

    /// <summary>由 <c>PreloadManager.LoadAssetSets</c> 补丁在注入 mod 资产后调用。</summary>
    public static void RecordInjectedAssets(string sessionName, int count)
    {
        if (!IsActive || string.IsNullOrEmpty(sessionName))
        {
            return;
        }

        lock (Gate)
        {
            InjectedAssets[sessionName] = count;
        }
    }

    #region 资源加载观测（由 Harmony 补丁调用）

    public static void OnSessionCreated(AssetLoadingSession session, string name, int requested)
    {
        if (!IsActive || session == null)
        {
            return;
        }

        try
        {
            lock (Gate)
            {
                var record = FindOpenSessionLocked(name);
                if (record == null || ElapsedMs - record.StartMs > 5000)
                {
                    record = new PreloadSessionRecord
                    {
                        Name = name,
                        StartMs = ElapsedMs
                    };
                    Sessions.Add(record);
                    if (Sessions.Count > MaxSessionRecords)
                    {
                        Sessions.RemoveAt(0);
                    }
                }

                if (record.Requested <= 0 && requested > 0)
                {
                    record.Requested = requested;
                }

                if (InjectedAssets.TryGetValue(name, out int injected))
                {
                    record.Injected = injected;
                }

                SessionMap[session] = record;
            }
        }
        catch
        {
            // 诊断代码绝不影响游戏。
        }
    }

    /// <summary>每个 <c>AssetLoadingSession.Process</c> 调用结束后累计主线程耗时。</summary>
    public static void OnSessionProcess(AssetLoadingSession session, long usec, bool completed)
    {
        if (!IsActive || session == null)
        {
            return;
        }

        try
        {
            lock (Gate)
            {
                if (!SessionMap.TryGetValue(session, out PreloadSessionRecord? record))
                {
                    return;
                }

                record.ProcessCalls++;
                record.MainThreadUsec += usec;
                if (usec > record.MaxProcessUsec)
                {
                    record.MaxProcessUsec = usec;
                }

                if (completed)
                {
                    record.Completed = true;
                    if (record.EndMs < 0)
                    {
                        record.EndMs = ElapsedMs;
                    }

                    SessionMap.Remove(session);
                }
            }
        }
        catch
        {
            // 诊断代码绝不影响游戏。
        }
    }

    /// <summary>记录一次主线程同步加载（缓存未命中）的耗时。</summary>
    public static void RecordSyncLoad(string path, long usec)
    {
        if (!IsActive || string.IsNullOrEmpty(path) || usec < 0)
        {
            return;
        }

        try
        {
            bool modOwned = IsModAsset(path);
            lock (Gate)
            {
                _syncLoadCount++;
                if (modOwned)
                {
                    _syncLoadModCount++;
                }

                _syncLoadTotalUsec += usec;
                if (usec > _syncLoadMaxUsec)
                {
                    _syncLoadMaxUsec = usec;
                }

                var record = new SyncLoadRecord(path, usec, ElapsedMs, modOwned);
                if (SyncLoads.Count < MaxSyncLoadRecords)
                {
                    SyncLoads.Add(record);
                    return;
                }

                int slowestReplacement = 0;
                for (int i = 1; i < SyncLoads.Count; i++)
                {
                    if (SyncLoads[i].Usec < SyncLoads[slowestReplacement].Usec)
                    {
                        slowestReplacement = i;
                    }
                }

                if (record.Usec > SyncLoads[slowestReplacement].Usec)
                {
                    SyncLoads[slowestReplacement] = record;
                }
            }
        }
        catch
        {
            // 诊断代码绝不影响游戏。
        }
    }

    #endregion

    #region 日志捕获

    /// <summary>
    /// 订阅 <see cref="Log.LogCallback" /> 的入口。必须便宜、无分配（热路径日志很少）且绝不抛异常。
    /// 注意：该回调运行在游戏全局日志锁内，因此本类 <c>Gate</c> 内**禁止**调用任何游戏 Logger，
    /// 保持「Logger 锁 → Gate」单向锁序，否则会死锁。
    /// </summary>
    public static void OnGameLog(LogLevel level, string text, int skipFrames)
    {
        if (!IsActive || string.IsNullOrEmpty(text))
        {
            return;
        }

        try
        {
            long at = ElapsedMs;
            bool isWarning = level >= LogLevel.Warn;
            lock (Gate)
            {
                if (isWarning)
                {
                    if (level == LogLevel.Error)
                    {
                        _errorCount++;
                    }
                    else
                    {
                        _warnCount++;
                    }

                    if (IsModRelated(text) && ModWarnings.Count < 32)
                    {
                        ModWarnings.Add(Trim(text, 400));
                    }
                }

                RecentLogs.Enqueue(new PerfLogLine(at, level, Trim(text, 500)));
                while (RecentLogs.Count > MaxLogLines)
                {
                    RecentLogs.Dequeue();
                }
            }

            ParseLogLine(text, at);
        }
        catch
        {
            // 日志回调里抛异常会打断游戏日志管线，必须吞掉。
        }
    }

    private static void ParseLogLine(string text, long at)
    {
        if (text.StartsWith("Preloading '", StringComparison.Ordinal))
        {
            ParsePreloadLine(text, at);
            return;
        }

        if (text.StartsWith("Asset not cached: ", StringComparison.Ordinal))
        {
            string path = text["Asset not cached: ".Length..].Trim();
            lock (Gate)
            {
                _notCachedCount++;
                if (IsModAsset(path))
                {
                    _notCachedModCount++;
                }
            }

            return;
        }

        if (text.StartsWith("[Startup] ", StringComparison.Ordinal))
        {
            AddMarkFromLog(at, "game:startup", text);
            return;
        }

        if (text.StartsWith("[AutoRegister] ", StringComparison.Ordinal))
        {
            AddMarkFromLog(at, "game:auto-register", text);
            return;
        }

        if (text.StartsWith("Finished mod initialization for ", StringComparison.Ordinal))
        {
            AddMarkFromLog(at, "game:mod-initialized", text);
            return;
        }

        if (text.StartsWith("Loading assembly DLL ", StringComparison.Ordinal))
        {
            AddMarkFromLog(at, "game:load-assembly", text);
            return;
        }

        if (text.StartsWith("Loading Godot PCK ", StringComparison.Ordinal))
        {
            AddMarkFromLog(at, "game:load-pck", text);
            return;
        }

        if (text.StartsWith("Calling initializer method of type ", StringComparison.Ordinal))
        {
            AddMarkFromLog(at, "game:mod-initializer", text);
            return;
        }

        if (text.Contains("[Content] Registered ", StringComparison.Ordinal))
        {
            lock (Gate)
            {
                if (text.Contains("[Content] Registered card:", StringComparison.Ordinal))
                {
                    _registeredCards++;
                }
                else if (text.Contains("[Content] Registered power:", StringComparison.Ordinal))
                {
                    _registeredPowers++;
                }
                else if (text.Contains("[Content] Registered relic:", StringComparison.Ordinal))
                {
                    _registeredRelics++;
                }
                else if (text.Contains("[Content] Registered character:", StringComparison.Ordinal))
                {
                    _registeredCharacters++;
                }
            }

            return;
        }

        if (text.Contains("RitsuLib Startup Audit", StringComparison.Ordinal))
        {
            AddMarkFromLog(at, "framework:startup-audit", null);
            return;
        }

        if (text.Contains("RitsuLib exclusive self-time", StringComparison.Ordinal))
        {
            AddMarkFromLog(at, "framework:startup-audit-total", text.Trim());
            return;
        }

        if (text.StartsWith("Threaded load status ", StringComparison.Ordinal) &&
            text.Contains("falling back to sync load", StringComparison.Ordinal))
        {
            lock (Gate)
            {
                _syncFallbackCount++;
            }
        }
    }

    private static void ParsePreloadLine(string text, long at)
    {
        int firstQuote = text.IndexOf('\'');
        if (firstQuote < 0)
        {
            return;
        }

        int secondQuote = text.IndexOf('\'', firstQuote + 1);
        if (secondQuote <= firstQuote)
        {
            return;
        }

        string name = text.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        if (text.Contains(" assets... ", StringComparison.Ordinal))
        {
            int requested = (int)Math.Min(int.MaxValue, ParseNumberAfter(text, "count="));
            int vfx = (int)Math.Min(int.MaxValue, ParseNumberAfter(text, "vfx="));
            lock (Gate)
            {
                var record = new PreloadSessionRecord
                {
                    Name = name,
                    Requested = requested,
                    StartMs = at,
                    Injected = InjectedAssets.TryGetValue(name, out int injected) ? injected : 0
                };
                Sessions.Add(record);
                if (Sessions.Count > MaxSessionRecords)
                {
                    Sessions.RemoveAt(0);
                }

                Volatile.Write(ref _activePreload, name);
                AddMarkLocked(new PerfMark(at, $"asset:preload-start:{name}", 0,
                    $"requested={requested} vfx={vfx} injected={record.Injected}"));
            }

            return;
        }

        if (text.Contains(" Complete: ", StringComparison.Ordinal))
        {
            long wallMs = ParseNumberAfter(text, "time_elapsed=");
            lock (Gate)
            {
                var record = FindOpenSessionLocked(name);
                if (record != null)
                {
                    record.EndMs = at;
                    record.Completed = true;
                    if (wallMs >= 0)
                    {
                        record.LogWallMs = wallMs;
                    }
                }

                if (ActivePreload == name)
                {
                    Volatile.Write(ref _activePreload, (string?)null);
                }

                AddMarkLocked(new PerfMark(at, $"asset:preload-end:{name}", wallMs,
                    record == null ? null : $"assets={record.Requested} mainthread={record.MainThreadMs:F1}ms"));
            }
        }
    }

    private static void AddMarkFromLog(long at, string label, string? detail)
    {
        try
        {
            lock (Gate)
            {
                AddMarkLocked(new PerfMark(at, label, 0, detail == null ? null : Trim(detail, 300)));
            }
        }
        catch
        {
            // 诊断代码绝不影响游戏。
        }
    }

    #endregion

    /// <summary>取最近几条日志（长帧上下文用；只在长帧时调用，允许分配）。</summary>
    public static string[] GetRecentLogTail(int count)
    {
        if (!IsActive)
        {
            return [];
        }

        lock (Gate)
        {
            int take = Math.Clamp(count, 0, RecentLogs.Count);
            if (take == 0)
            {
                return [];
            }

            return RecentLogs.ToArray()[^take..].Select(line => line.Text).ToArray();
        }
    }

    /// <summary>复制一份当前数据；写报告时只读，避免长时间持锁。</summary>
    public static PerfTimelineSnapshot Snapshot()
    {
        if (!IsActive)
        {
            return new PerfTimelineSnapshot();
        }

        lock (Gate)
        {
            var snapshot = new PerfTimelineSnapshot
            {
                EngineOffsetMsec = _engineOffsetMsec,
                StartMsec = _startMsec,
                Phase = _phase,
                NotCachedCount = _notCachedCount,
                NotCachedModCount = _notCachedModCount,
                SyncLoadCount = _syncLoadCount,
                SyncLoadModCount = _syncLoadModCount,
                SyncLoadTotalUsec = _syncLoadTotalUsec,
                SyncLoadMaxUsec = _syncLoadMaxUsec,
                SyncFallbackCount = _syncFallbackCount,
                WarnCount = _warnCount,
                ErrorCount = _errorCount,
                RegisteredCards = _registeredCards,
                RegisteredPowers = _registeredPowers,
                RegisteredRelics = _registeredRelics,
                RegisteredCharacters = _registeredCharacters
            };

            snapshot.Marks.AddRange(Marks);
            snapshot.RecentLogs.AddRange(RecentLogs);
            foreach (PreloadSessionRecord? session in Sessions)
            {
                snapshot.Sessions.Add(new PreloadSessionRecord
                {
                    Name = session.Name,
                    Requested = session.Requested,
                    Injected = session.Injected,
                    StartMs = session.StartMs,
                    EndMs = session.EndMs,
                    LogWallMs = session.LogWallMs,
                    MainThreadUsec = session.MainThreadUsec,
                    MaxProcessUsec = session.MaxProcessUsec,
                    ProcessCalls = session.ProcessCalls,
                    Completed = session.Completed
                });
            }

            snapshot.SyncLoads.AddRange(SyncLoads);
            snapshot.ModWarnings.AddRange(ModWarnings);
            foreach (KeyValuePair<string, int> pair in InjectedAssets)
            {
                snapshot.InjectedAssets[pair.Key] = pair.Value;
            }

            return snapshot;
        }
    }

    private static PreloadSessionRecord? FindOpenSessionLocked(string name)
    {
        for (int i = Sessions.Count - 1; i >= 0; i--)
        {
            var session = Sessions[i];
            if (session.Name == name && !session.Completed)
            {
                return session;
            }
        }

        return null;
    }

    private static void MarkMeasured(string label, long elapsedMs, string? detail)
    {
        try
        {
            lock (Gate)
            {
                AddMarkLocked(new PerfMark(ElapsedMs, label, elapsedMs, detail));
            }
        }
        catch
        {
            // 诊断代码绝不影响游戏。
        }
    }

    private static void AddMarkLocked(PerfMark mark)
    {
        if (Marks.Count >= MaxMarkCount)
        {
            Marks.RemoveAt(0);
        }

        Marks.Add(mark);
    }

    private static long ElapsedMsSince(long startTimestamp)
    {
        return (long)Math.Round((Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency);
    }

    /// <summary>从 <paramref name="text" /> 的 <paramref name="key" /> 后解析一个整数（允许千分位逗号）。</summary>
    private static long ParseNumberAfter(string text, string key)
    {
        int index = text.IndexOf(key, StringComparison.Ordinal);
        if (index < 0)
        {
            return -1;
        }

        index += key.Length;
        long value = 0;
        int digits = 0;
        while (index < text.Length)
        {
            char c = text[index];
            if (c == ',')
            {
                index++;
                continue;
            }

            if (c is < '0' or > '9')
            {
                break;
            }

            value = value * 10 + (c - '0');
            digits++;
            index++;
            if (digits > 12)
            {
                break;
            }
        }

        return digits == 0 ? -1 : value;
    }

    private static bool IsModAsset(string path)
    {
        return path.StartsWith(ModAssetPrefixCore, StringComparison.Ordinal) ||
               path.StartsWith(ModAssetPrefixCrychic, StringComparison.Ordinal);
    }

    private static bool IsModRelated(string text)
    {
        return text.Contains("BangDream", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("ItsCrychic", StringComparison.OrdinalIgnoreCase) ||
               text.Contains(ModAssetPrefixCore, StringComparison.OrdinalIgnoreCase) ||
               text.Contains(ModAssetPrefixCrychic, StringComparison.OrdinalIgnoreCase);
    }

    private static string Trim(string text, int maxLength)
    {
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}

/// <summary><see cref="PerfTimeline.Snapshot" /> 导出的只读数据副本。</summary>
internal sealed class PerfTimelineSnapshot
{
    public long EngineOffsetMsec;
    public long StartMsec;
    public string Phase = "";
    public long NotCachedCount;
    public long NotCachedModCount;
    public long SyncLoadCount;
    public long SyncLoadModCount;
    public long SyncLoadTotalUsec;
    public long SyncLoadMaxUsec;
    public long SyncFallbackCount;
    public int WarnCount;
    public int ErrorCount;
    public int RegisteredCards;
    public int RegisteredPowers;
    public int RegisteredRelics;
    public int RegisteredCharacters;
    public List<PerfMark> Marks = new();
    public List<PerfLogLine> RecentLogs = new();
    public List<PreloadSessionRecord> Sessions = new();
    public List<SyncLoadRecord> SyncLoads = new();
    public List<string> ModWarnings = new();
    public Dictionary<string, int> InjectedAssets = new();
}
