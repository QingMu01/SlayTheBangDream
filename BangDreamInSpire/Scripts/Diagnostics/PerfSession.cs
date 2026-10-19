using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using STS2RitsuLib;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace BangDreamInSpire.Scripts.Diagnostics;

/// <summary>
/// BDS-Perf 的总装配：读取 RitsuLib 设置页的开关、订阅生命周期事件、挂帧采样器、按需写报告。
/// 默认关闭且只在初始化时判断一次：关闭状态下不订阅日志、不注册补丁、不挂采样节点，
/// 因此对性能与内存零影响；在设置页打开后需要重启游戏才会开始检测。
/// </summary>
internal static class PerfSession
{
    /// <summary>长帧阈值（毫秒）。</summary>
    public const int FrameSpikeThresholdMs = 50;

    /// <summary>周期检查间隔（秒）；只有检测到异常长帧时才真正落盘。</summary>
    public const int PeriodicReportSeconds = 180;

    /// <summary>保留的历史报告份数。</summary>
    public const int RetainReports = 6;

    private static readonly object Gate = new();
    private static readonly List<IDisposable> Subscriptions = new();
    private static PerfConfig _config = new();
    private static Logger? _logger;
    private static readonly object WriteGate = new();
    private static Task _writeChain = Task.CompletedTask;
    private static int _initialized;
    private static int _dumping;
    private static string? _lastReportPath;

    public static PerfConfig Config => _config;

    public static bool Enabled => _config.Enabled;

    public static PerfFrameSampler? Sampler { get; private set; }

    public static Logger? LoggerRef => _logger;

    public static string? LastReportPath => _lastReportPath;

    /// <summary>
    /// 在 <c>BangDreamInSpireCore.Initialize</c> 的最开头调用。
    /// <paramref name="config" /> 是 RitsuLib 数据槽里的设置实例；关闭时直接返回，不安装任何东西。
    /// </summary>
    public static void Initialize(Logger logger, PerfConfig config)
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        _logger = logger;
        _config = config ?? new PerfConfig();

        if (!_config.Enabled)
        {
            _logger.Info("[BDS-Perf] 未启用（RitsuLib 设置页开启后重启游戏生效）");
            return;
        }

        PerfTimeline.Start();

        try
        {
            Log.LogCallback += PerfTimeline.OnGameLog;
        }
        catch (Exception ex)
        {
            _logger.Warn($"[BDS-Perf] 无法订阅游戏日志: {ex.Message}");
        }

        SubscribeLifecycleEvents();

        _logger.Info(
            $"[BDS-Perf] 已启用: spike_threshold={FrameSpikeThresholdMs}ms, " +
            $"report_dir={PerfReporter.ReportDirectory}");
    }

    private static void SubscribeLifecycleEvents()
    {
        Sub(RitsuLibFramework.SubscribeLifecycle<FrameworkInitializedEvent>(_ =>
            PerfTimeline.Mark("framework:initialized")));

        Sub(RitsuLibFramework.SubscribeLifecycle<EssentialInitializationStartingEvent>(_ =>
            PerfTimeline.Mark("game:essential-init-start")));
        Sub(RitsuLibFramework.SubscribeLifecycle<EssentialInitializationCompletedEvent>(_ =>
            PerfTimeline.Mark("game:essential-init-end")));

        Sub(RitsuLibFramework.SubscribeLifecycle<ModelRegistryInitializingEvent>(_ =>
            PerfTimeline.Mark("game:model-registry-start")));
        Sub(RitsuLibFramework.SubscribeLifecycle<ModelRegistryInitializedEvent>(_ =>
            PerfTimeline.Mark("game:model-registry-end")));

        Sub(RitsuLibFramework.SubscribeLifecycle<ModelIdsInitializingEvent>(_ =>
            PerfTimeline.Mark("game:model-ids-start")));
        Sub(RitsuLibFramework.SubscribeLifecycle<ModelIdsInitializedEvent>(_ =>
            PerfTimeline.Mark("game:model-ids-end")));

        Sub(RitsuLibFramework.SubscribeLifecycle<ModelPreloadingStartingEvent>(_ =>
            PerfTimeline.Mark("game:model-preload-start")));
        Sub(RitsuLibFramework.SubscribeLifecycle<ModelPreloadingCompletedEvent>(_ =>
            PerfTimeline.Mark("game:model-preload-end")));

        Sub(RitsuLibFramework.SubscribeLifecycle<DeferredInitializationStartingEvent>(_ =>
            PerfTimeline.Mark("game:deferred-init-start")));
        Sub(RitsuLibFramework.SubscribeLifecycle<DeferredInitializationCompletedEvent>(_ =>
            PerfTimeline.Mark("game:deferred-init-end")));

        Sub(RitsuLibFramework.SubscribeLifecycle<GameTreeEnteredEvent>(e =>
        {
            PerfTimeline.Mark("game:tree-entered");
            // 比 GameReady 更早挂上采样器；replay 保证晚订阅也能立刻拿到 NGame。
            AttachSampler(e.Game);
        }));

        Sub(RitsuLibFramework.SubscribeLifecycle<GameReadyEvent>(e =>
            AttachSampler(e.Game)));

        Sub(RitsuLibFramework.SubscribeLifecycle<MainMenuReadyEvent>(_ =>
            PerfTimeline.OnMainMenuReady()));

        Sub(RitsuLibFramework.SubscribeLifecycle<RunStartedEvent>(_ =>
        {
            PerfTimeline.SetPhase("run");
            PerfTimeline.Mark("event:run-started");
        }));
        Sub(RitsuLibFramework.SubscribeLifecycle<RunLoadedEvent>(_ =>
        {
            PerfTimeline.SetPhase("run");
            PerfTimeline.Mark("event:run-loaded");
        }));
        Sub(RitsuLibFramework.SubscribeLifecycle<RunEndedEvent>(_ =>
        {
            PerfTimeline.SetPhase("main-menu");
            PerfTimeline.Mark("event:run-ended");
            Dump("run-ended");
        }));

        Sub(RitsuLibFramework.SubscribeLifecycle<CombatStartingEvent>(e =>
        {
            string encounter = "?";
            try
            {
                encounter = e.CombatState?.Encounter?.Id.Entry ?? "?";
            }
            catch
            {
                // 某些战斗没有 Encounter（测试/特殊房间），保持默认值。
            }

            PerfTimeline.SetPhase($"combat:{encounter}");
            PerfTimeline.Mark("event:combat-start", encounter);
        }));
        Sub(RitsuLibFramework.SubscribeLifecycle<CombatEndedEvent>(_ =>
        {
            PerfTimeline.SetPhase("run");
            PerfTimeline.Mark("event:combat-end");
        }));

        Sub(RitsuLibFramework.SubscribeLifecycle<RoomEnteredEvent>(e =>
        {
            PerfTimeline.SetPhase($"room:{e.Room.RoomType}");
            PerfTimeline.Mark("event:room-entered", e.Room.ModelId?.Entry);
        }));
    }

    private static void Sub(IDisposable subscription)
    {
        lock (Gate)
        {
            Subscriptions.Add(subscription);
        }
    }

    private static void AttachSampler(NGame game)
    {
        if (Sampler != null || !_config.Enabled)
        {
            return;
        }

        try
        {
            var sampler = new PerfFrameSampler();
            game.AddChildSafely(sampler);
            Sampler = sampler;
            PerfTimeline.Mark("perf:sampler-attached");
        }
        catch (Exception ex)
        {
            _logger?.Warn($"[BDS-Perf] 帧采样器挂载失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 写一份性能报告。返回 markdown 路径；失败或正在写时返回上一次的路径。
    /// <paramref name="sync" /> 为 false 时，主线程只采集数据并序列化，写盘丢到后台串行队列，
    /// 避免监测自己在启动/掉帧时刻制造磁盘尖峰。退出/控制台路径使用同步写，确保拿到文件。
    /// </summary>
    public static string? Dump(string reason, bool sync = false)
    {
        if (!_config.Enabled)
        {
            return _lastReportPath;
        }

        if (Interlocked.Exchange(ref _dumping, 1) == 1)
        {
            return _lastReportPath;
        }

        try
        {
            PerfReportPayload? payload = PerfReporter.Prepare(reason, _config, Sampler);
            if (payload == null)
            {
                return _lastReportPath;
            }

            _lastReportPath = payload.MarkdownPath;
            if (sync)
            {
                PerfReporter.WriteFiles(payload);
            }
            else
            {
                EnqueueWrite(payload);
            }

            return payload.MarkdownPath;
        }
        catch (Exception ex)
        {
            _logger?.Warn($"[BDS-Perf] 写报告失败: {ex}");
            return _lastReportPath;
        }
        finally
        {
            Volatile.Write(ref _dumping, 0);
        }
    }

    private static void EnqueueWrite(PerfReportPayload payload)
    {
        lock (WriteGate)
        {
            _writeChain = _writeChain.ContinueWith(
                _ =>
                {
                    try
                    {
                        PerfReporter.WriteFiles(payload);
                    }
                    catch
                    {
                        // 后台写盘失败不应影响游戏；latest 会在下一次报告时覆盖。
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
    }

    public static void OnSamplerExit()
    {
        Sampler = null;
    }

    /// <summary>退出/同步写盘前等待后台写队列排空，避免旧 payload 覆盖 perf-latest。</summary>
    public static void FlushPendingWrites(int timeoutMs = 2000)
    {
        Task chain;
        lock (WriteGate)
        {
            chain = _writeChain;
        }

        try
        {
            chain.Wait(timeoutMs);
        }
        catch
        {
            // 等待超时也不阻塞退出；timestamped 报告不会丢。
        }
    }

}
