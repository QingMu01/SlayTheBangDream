using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using STS2RitsuLib;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace BangDreamInSpire.Scripts.Diagnostics;

/// <summary>按需装配性能诊断，默认关闭；设置变更需重启。</summary>
internal static class PerfSession
{
    public const int FrameSpikeThresholdMs = 50;

    public const int PeriodicReportSeconds = 180;

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

    public static string? LastReportPath => Volatile.Read(ref _lastReportPath);

    /// <summary>初始化诊断；关闭时不安装任何监听或补丁。</summary>
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
            // 尽早挂采样器，replay 用于晚订阅。
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
                // 无 Encounter 时保持默认值。
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

    /// <summary>生成报告；异步模式后台写盘，同步模式用于退出与控制台。</summary>
    public static string? Dump(string reason, bool sync = false)
    {
        if (!_config.Enabled)
        {
            return LastReportPath;
        }

        if (Interlocked.Exchange(ref _dumping, 1) == 1)
        {
            return LastReportPath;
        }

        try
        {
            PerfReportPayload? payload = PerfReporter.Prepare(reason, _config, Sampler);
            if (payload == null)
            {
                return LastReportPath;
            }

            // 用 Volatile 保证后台可见性。
            Volatile.Write(ref _lastReportPath, payload.MarkdownPath);
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
            return LastReportPath;
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
                        // 后台写盘失败不影响游戏。
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

    /// <summary>解除诊断订阅，供未来卸载或热重载使用。</summary>
    public static void Shutdown()
    {
        try
        {
            Log.LogCallback -= PerfTimeline.OnGameLog;
        }
        catch (Exception ex)
        {
            _logger?.Warn($"[BDS-Perf] 解除日志订阅失败: {ex.Message}");
        }

        lock (Gate)
        {
            foreach (var subscription in Subscriptions)
            {
                try
                {
                    subscription.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"[BDS-Perf] 释放生命周期订阅失败: {ex.Message}");
                }
            }

            Subscriptions.Clear();
        }
    }

    /// <summary>等待后台写队列排空。</summary>
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
            // 超时不阻塞退出。
        }
    }

}
