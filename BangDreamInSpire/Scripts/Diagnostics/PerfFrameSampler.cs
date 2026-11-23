using Godot;

namespace BangDreamInSpire.Scripts.Diagnostics;

/// <summary>零分配逐帧采样、每秒聚合，长帧时保留前后上下文；报告由 PerfSession 写出。</summary>
public partial class PerfFrameSampler : Node
{
    private const int FrameCapacity = 4096;
    private const int MaxSpikes = 64;
    private const int MaxSeconds = 600;
    private const int MedianWindow = 120;
    private const long StartupReportDelayUsec = 20_000_000;
    private const long SuspendedFrameUsec = 2_000_000;

    /// <summary>根视口 GPU 计时是否成功启用。</summary>
    internal static bool GpuTimingEnabled { get; private set; }

    private readonly int[] _frameUs = new int[FrameCapacity];
    private readonly int[] _medianBuffer = new int[MedianWindow];
    private readonly List<FrameSpike> _spikes = new(MaxSpikes);
    private readonly List<SecondSample> _seconds = new(MaxSeconds);
    private readonly Dictionary<string, PhaseFrameStats> _phaseStats = new();
    private readonly int[] _histogram = new int[5001];
    private long _histogramFrames;

    private int _frameCursor;
    private int _frameCount;
    private long _lastUsec;
    private long _secondStartUsec;
    private int _secondFrames;
    private long _secondTotalUs;
    private long _secondMaxUs;
    private int _secondOver33;
    private int _secondOver50;
    private int _secondOver100;
    private int _secondMicroStutters;
    private long _secondAllocBytes;
    private int _secondNodesAdded;
    private long _lastAllocBytes;
    private long _lastFrameAllocBytes;
    private long _nodesAtLastSecond;
    private long _shaderCompilesAtLastSecond;
    private long _pipelineAtLastSecond;
    private long _textureMemAtLastSecond;
    private int _gc0AtLastSecond;
    private long _backgroundFrames;
    private long _backgroundUsec;
    private long _backgroundSinceUsec;
    private long _lastPreDrawUsec;
    private long _lastPostDrawUsec;
    private long _renderSubmitUs;
    private long _presentWaitUs;
    private double _lastFrameSetupCpuMs;
    private double _secondFrameSetupMsSum;
    private int _secondFrameSetupSamples;
    private double _refreshRateHz = 60.0;
    private int _cachedViewportNodes;
    private int _cachedCanvasGroupNodes;

    private bool _gpuTimingEnabled;
    private Rid _rootViewportRid;
    private double _lastGpuMs;
    private double _secondGpuMsSum;
    private int _secondGpuSamples;
    private double _secondGpuMsMax;
    private long _gcPauseTicksAtLastSecond;
    private int _gc2AtLastSecond;
    private long _totalFrames;
    private long _totalUs;
    private long _maxUs;
    private long _spikeTotal;
    private long _dynamicSpikeThresholdUs;
    private long _microStutterThresholdUs;
    private long _mainMenuUsec = -1;
    private bool _startupReported;
    private long _lastPeriodicReportUsec;

    // 上一帧结束时的上下文（不分配：字符串是引用，场景是节点引用），用于记录「卡住之前」的状态。
    private Node? _contextScene;
    private string _contextPhase = "startup";
    private string _contextLoading = "";

    public override void _Ready()
    {
        long now = (long)Time.GetTicksUsec();
        _lastUsec = now;
        _secondStartUsec = now;
        _lastPeriodicReportUsec = now;
        _lastAllocBytes = GC.GetAllocatedBytesForCurrentThread();
        _dynamicSpikeThresholdUs = Math.Max(20, PerfSession.FrameSpikeThresholdMs) * 1000L;
        _microStutterThresholdUs = _dynamicSpikeThresholdUs;
        _gc0AtLastSecond = GC.CollectionCount(0);
        _pipelineAtLastSecond = ReadPipelineCompilations();
        _textureMemAtLastSecond = ReadTextureMem();
        _gcPauseTicksAtLastSecond = GC.GetTotalPauseDuration().Ticks;
        _gc2AtLastSecond = GC.CollectionCount(2);
        _refreshRateHz = ReadRefreshRateHz();

        SetupGpuTiming();

        try
        {
            RenderingServer.FramePreDraw += OnFramePreDraw;
            RenderingServer.FramePostDraw += OnFramePostDraw;
        }
        catch
        {
        }
        try
        {
            _nodesAtLastSecond = (long)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
            _shaderCompilesAtLastSecond = (long)Performance.GetMonitor(Performance.Monitor.PipelineCompilationsCanvas);
        }
        catch
        {
        }

        try
        {
            // 以节点新增近似场景实例化成本。
            GetTree().NodeAdded += OnNodeAdded;
        }
        catch
        {
        }

        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (!PerfSession.Enabled)
        {
            long nowDisabled = (long)Time.GetTicksUsec();
            _lastUsec = nowDisabled;
            ResetSecondWindow(nowDisabled);
            return;
        }

        if (!IsWindowFocused())
        {
            // 排除失焦限帧。
            long nowBackground = (long)Time.GetTicksUsec();
            if (_backgroundSinceUsec == 0)
            {
                _backgroundSinceUsec = nowBackground;
            }

            _backgroundFrames++;
            _lastUsec = nowBackground;
            ResetSecondWindow(nowBackground);
            UpdateContext();
            return;
        }

        if (_backgroundSinceUsec != 0)
        {
            long nowFocus = (long)Time.GetTicksUsec();
            _backgroundUsec += nowFocus - _backgroundSinceUsec;
            _backgroundSinceUsec = 0;
            _lastUsec = nowFocus;
            ResetSecondWindow(nowFocus);
            UpdateContext();
            return;
        }

        long now = (long)Time.GetTicksUsec();
        long usec = now - _lastUsec;
        _lastUsec = now;
        if (usec <= 0)
        {
            return;
        }

        if (usec > SuspendedFrameUsec)
        {
            // 挂起后重置采样窗口。
            ResetSecondWindow(now);
            UpdateContext();
            return;
        }

        long alloc = GC.GetAllocatedBytesForCurrentThread();
        long allocDelta = alloc - _lastAllocBytes;
        if (allocDelta < 0)
        {
            allocDelta = 0;
        }

        _lastAllocBytes = alloc;
        _lastFrameAllocBytes = allocDelta;

        if (usec > int.MaxValue)
        {
            usec = int.MaxValue;
        }

        long preDraw = Volatile.Read(ref _lastPreDrawUsec);
        long postDraw = Volatile.Read(ref _lastPostDrawUsec);
        _renderSubmitUs = preDraw > 0 && postDraw >= preDraw ? postDraw - preDraw : 0;
        _presentWaitUs = postDraw > 0 && now >= postDraw ? now - postDraw : 0;

        // CPU setup 无需开启测量，失败按 0；原始单位为 ms。
        _lastFrameSetupCpuMs = ReadFrameSetupCpuMs();
        if (_lastFrameSetupCpuMs >= 0)
        {
            _secondFrameSetupMsSum += _lastFrameSetupCpuMs;
            _secondFrameSetupSamples++;
        }
        else
        {
            _lastFrameSetupCpuMs = 0;
        }

        // GPU 测量默认关闭，失败按 0。
        if (_gpuTimingEnabled)
        {
            _lastGpuMs = ReadGpuRenderTimeMs(_rootViewportRid);
            _secondGpuMsSum += _lastGpuMs;
            _secondGpuSamples++;
            if (_lastGpuMs > _secondGpuMsMax)
            {
                _secondGpuMsMax = _lastGpuMs;
            }
        }

        _histogram[Math.Min(usec / 1000L, 5000L)]++;
        _histogramFrames++;

        _frameUs[_frameCursor] = (int)usec;
        _frameCursor = (_frameCursor + 1) % FrameCapacity;
        if (_frameCount < FrameCapacity)
        {
            _frameCount++;
        }

        _totalFrames++;
        _totalUs += usec;
        if (usec > _maxUs)
        {
            _maxUs = usec;
        }

        _secondFrames++;
        _secondTotalUs += usec;
        if (usec > _secondMaxUs)
        {
            _secondMaxUs = usec;
        }

        if (allocDelta > 0)
        {
            _secondAllocBytes += allocDelta;
        }

        if (usec >= 33_333)
        {
            _secondOver33++;
        }

        if (usec >= 50_000)
        {
            _secondOver50++;
        }

        if (usec >= 100_000)
        {
            _secondOver100++;
        }

        if (usec >= _microStutterThresholdUs)
        {
            _secondMicroStutters++;
        }

        if (usec >= _dynamicSpikeThresholdUs)
        {
            CaptureSpike(usec, _lastFrameSetupCpuMs);
        }

        if (now - _secondStartUsec >= 1_000_000)
        {
            FlushSecond(now);
        }

        UpdateContext();
    }

    public override void _ExitTree()
    {
        if (_gpuTimingEnabled)
        {
            try
            {
                RenderingServer.ViewportSetMeasureRenderTime(_rootViewportRid, false);
            }
            catch
            {
            }

            _gpuTimingEnabled = false;
            GpuTimingEnabled = false;
        }

        try
        {
            GetTree().NodeAdded -= OnNodeAdded;
        }
        catch
        {
        }

        try
        {
            RenderingServer.FramePreDraw -= OnFramePreDraw;
            RenderingServer.FramePostDraw -= OnFramePostDraw;
        }
        catch
        {
        }

        try
        {
        // 后排空写队列并同步退出报告，最多等待 300ms。
            PerfSession.FlushPendingWrites(timeoutMs: 300);
            PerfSession.Dump("exit", sync: true);
        }
        catch
        {
            }
        finally
        {
            PerfSession.OnSamplerExit();
        }
    }

    /// <summary>导出写报告用的帧快照。</summary>
    internal PerfFrameSnapshot Snapshot()
    {
        var snapshot = new PerfFrameSnapshot
        {
            TotalFrames = _totalFrames,
            WindowFrames = _frameCount,
            BackgroundFrames = _backgroundFrames,
            BackgroundMs = (_backgroundUsec +
                            (_backgroundSinceUsec == 0 ? 0L : (long)Time.GetTicksUsec() - _backgroundSinceUsec)) / 1000,
            SpikeCount = _spikeTotal,
            MaxMs = _maxUs / 1000.0,
            AvgMs = _totalFrames == 0 ? 0 : _totalUs / 1000.0 / _totalFrames
        };

        snapshot.WindowFrames = (int)Math.Min(int.MaxValue, _histogramFrames);
        if (_histogramFrames > 0)
        {
            // 桶号已经是毫秒，不能再除以 1000。
            snapshot.P50Ms = HistogramPercentile(50);
            snapshot.P95Ms = HistogramPercentile(95);
            snapshot.P99Ms = HistogramPercentile(99);
        }

        snapshot.Spikes = new List<FrameSpike>(_spikes);
        snapshot.Spikes.Sort((a, b) => b.Ms.CompareTo(a.Ms));
        snapshot.Seconds = new List<SecondSample>(_seconds);
        foreach (KeyValuePair<string, PhaseFrameStats> pair in _phaseStats)
        {
            snapshot.PhaseStats[pair.Key] = new PhaseFrameStats
            {
                Frames = pair.Value.Frames,
                TotalMs = pair.Value.TotalMs,
                MaxMs = pair.Value.MaxMs
            };
        }

        return snapshot;
    }

    private static int PercentileIndex(int count, int percentile)
    {
        int index = (int)(count * (long)percentile / 100L);
        return Math.Clamp(index, 0, count - 1);
    }

    private void OnNodeAdded(Node node)
    {
        _secondNodesAdded++;
    }

    private void UpdateContext()
    {
        try
        {
            _contextScene = GetTree()?.CurrentScene;
        }
        catch
        {
            _contextScene = null;
        }

        _contextPhase = PerfTimeline.CurrentPhase;
        _contextLoading = PerfTimeline.ActivePreload ?? "";
    }

    private void CaptureSpike(long usec, double frameSetupCpuMs)
    {
        _spikeTotal++;
        double ms = usec / 1000.0;
        long nodes = 0;
        long drawCalls = 0;
        long shaderCompiles = 0;

        try
        {
            nodes = (long)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
            drawCalls = (long)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
            shaderCompiles = (long)Performance.GetMonitor(Performance.Monitor.PipelineCompilationsCanvas);
        }
        catch
        {
        }

        int gc0 = 0;
        long managedBytes = 0;
        try
        {
            gc0 = GC.CollectionCount(0);
            managedBytes = GC.GetTotalMemory(false);
        }
        catch
        {
        }

        var spike = new FrameSpike
        {
            AtMs = PerfTimeline.ElapsedMs,
            Ms = ms,
            PhaseBefore = _contextPhase,
            PhaseAfter = PerfTimeline.CurrentPhase,
            SceneBefore = SafeSceneName(_contextScene),
            SceneAfter = SafeSceneName(GetTree()?.CurrentScene),
            LoadingBefore = _contextLoading,
            LoadingAfter = PerfTimeline.ActivePreload ?? "",
            Nodes = nodes,
            NodeDelta = nodes - _nodesAtLastSecond,
            DrawCalls = drawCalls,
            ShaderCompileDelta = shaderCompiles - _shaderCompilesAtLastSecond,
            PipelineDelta = (int)Math.Max(0, ReadPipelineCompilations() - _pipelineAtLastSecond),
            TextureMemBytes = ReadTextureMem(),
            TextureMemDelta = ReadTextureMem() - _textureMemAtLastSecond,
            Gc0Delta = gc0 - _gc0AtLastSecond,
            AllocBytes = _lastFrameAllocBytes,
            ManagedBytes = managedBytes,
            RenderSubmitUs = _renderSubmitUs,
            PresentWaitUs = _presentWaitUs,
            FrameSetupCpuUs = (long)Math.Round(Math.Max(0, frameSetupCpuMs) * 1000.0),
            PresentWaitedPeriods = PresentPeriods(_presentWaitUs, _refreshRateHz),
            GpuRenderTimeMs = _lastGpuMs,
            GpuPeriods = GpuPeriods(_lastGpuMs, _refreshRateHz),
            ViewportNodes = _cachedViewportNodes,
            CanvasGroupNodes = _cachedCanvasGroupNodes,
            Gc2Delta = Math.Max(0, GC.CollectionCount(2) - _gc2AtLastSecond),
            GcPauseMs = Math.Max(0, (GC.GetTotalPauseDuration().Ticks - _gcPauseTicksAtLastSecond)
                                    / (double)TimeSpan.TicksPerMillisecond),
            RecentLog = BuildRecentLog()
        };

        if (_spikes.Count < MaxSpikes)
        {
            _spikes.Add(spike);
            return;
        }

        int lightest = 0;
        for (int i = 1; i < _spikes.Count; i++)
        {
            if (_spikes[i].Ms < _spikes[lightest].Ms)
            {
                lightest = i;
            }
        }

        if (spike.Ms > _spikes[lightest].Ms)
        {
            _spikes[lightest] = spike;
        }
    }

    private void FlushSecond(long now)
    {
        long elapsedUs = now - _secondStartUsec;
        if (elapsedUs <= 0)
        {
            _secondStartUsec = now;
            return;
        }

        int frames = _secondFrames;
        double fps = frames * 1_000_000.0 / elapsedUs;
        double avgMs = frames == 0 ? 0 : _secondTotalUs / 1000.0 / frames;
        double maxMs = _secondMaxUs / 1000.0;
        string phase = PerfTimeline.CurrentPhase;
        long nodes = 0;
        long orphanNodes = 0;
        long drawCalls = 0;
        long staticMem = 0;
        long videoMem = 0;
        long shaderCompiles = 0;

        try
        {
            nodes = (long)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
            orphanNodes = (long)Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
            drawCalls = (long)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
            staticMem = (long)OS.GetStaticMemoryUsage();
            videoMem = (long)Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed);
            shaderCompiles = (long)Performance.GetMonitor(Performance.Monitor.PipelineCompilationsCanvas);
        }
        catch
        {
        }

        // 每秒统计渲染节点，供长帧引用上一秒缓存。
        CountSceneNodes(out int viewportNodes, out int canvasGroupNodes, out int worldEnvironmentNodes);
        _cachedViewportNodes = viewportNodes;
        _cachedCanvasGroupNodes = canvasGroupNodes;
        _refreshRateHz = ReadRefreshRateHz();

        double frameSetupCpuMsAvg = _secondFrameSetupSamples == 0
            ? 0
            : _secondFrameSetupMsSum / _secondFrameSetupSamples;

        // 未启用 GPU 时采样值保持 0。
        double gpuRenderTimeMsAvg = _secondGpuSamples == 0 ? 0 : _secondGpuMsSum / _secondGpuSamples;

        var sample = new SecondSample
        {
            AtMs = PerfTimeline.ElapsedMs,
            Frames = frames,
            MaxMs = maxMs,
            AvgMs = avgMs,
            Fps = fps,
            Phase = phase,
            Nodes = nodes,
            OrphanNodes = orphanNodes,
            DrawCalls = drawCalls,
            StaticMemBytes = staticMem,
            VideoMemBytes = videoMem,
            AllocBytes = _secondAllocBytes,
            NodesAdded = _secondNodesAdded,
            ShaderCompiles = (int)Math.Max(0, shaderCompiles - _shaderCompilesAtLastSecond),
            PipelineDelta = (int)Math.Max(0, ReadPipelineCompilations() - _pipelineAtLastSecond),
            TextureMemBytes = ReadTextureMem(),
            TextureMemDelta = ReadTextureMem() - _textureMemAtLastSecond,
            Over33 = _secondOver33,
            Over50 = _secondOver50,
            Over100 = _secondOver100,
            MicroStutters = _secondMicroStutters,
            GcPauseMs = Math.Max(0, (GC.GetTotalPauseDuration().Ticks - _gcPauseTicksAtLastSecond)
                                   / (double)TimeSpan.TicksPerMillisecond),
            Gc2Delta = Math.Max(0, GC.CollectionCount(2) - _gc2AtLastSecond),
            FrameSetupCpuMsAvg = frameSetupCpuMsAvg,
            GpuRenderTimeMsAvg = gpuRenderTimeMsAvg,
            GpuRenderTimeMsMax = _secondGpuMsMax,
            ViewportNodes = viewportNodes,
            CanvasGroupNodes = canvasGroupNodes,
            WorldEnvironmentNodes = worldEnvironmentNodes
        };

        _seconds.Add(sample);
        if (_seconds.Count > MaxSeconds)
        {
            _seconds.RemoveAt(0);
        }

        if (!_phaseStats.TryGetValue(phase, out PhaseFrameStats? stats))
        {
            stats = new PhaseFrameStats();
            _phaseStats[phase] = stats;
        }

        stats.Frames += frames;
        stats.TotalMs += _secondTotalUs / 1000.0;
        if (maxMs > stats.MaxMs)
        {
            stats.MaxMs = maxMs;
        }

        _nodesAtLastSecond = nodes;
        _shaderCompilesAtLastSecond = shaderCompiles;
        _pipelineAtLastSecond = ReadPipelineCompilations();
        _textureMemAtLastSecond = ReadTextureMem();
        _gc0AtLastSecond = GC.CollectionCount(0);
        _gcPauseTicksAtLastSecond = GC.GetTotalPauseDuration().Ticks;
        _gc2AtLastSecond = GC.CollectionCount(2);
        UpdateDynamicThreshold();

        _secondStartUsec = now;
        _secondFrames = 0;
        _secondTotalUs = 0;
        _secondMaxUs = 0;
        _secondOver33 = 0;
        _secondOver50 = 0;
        _secondOver100 = 0;
        _secondMicroStutters = 0;
        _secondAllocBytes = 0;
        _secondNodesAdded = 0;
        _secondFrameSetupMsSum = 0;
        _secondFrameSetupSamples = 0;
        _secondGpuMsSum = 0;
        _secondGpuSamples = 0;
        _secondGpuMsMax = 0;

        MaybeWriteAutoReport(now);
    }

    private void UpdateDynamicThreshold()
    {
        int count = Math.Min(_frameCount, MedianWindow);
        if (count < 20)
        {
            _dynamicSpikeThresholdUs = Math.Max(20, PerfSession.FrameSpikeThresholdMs) * 1000L;
            _microStutterThresholdUs = _dynamicSpikeThresholdUs;
            return;
        }

        int start = _frameCursor - count;
        if (start < 0)
        {
            start += FrameCapacity;
        }

        for (int i = 0; i < count; i++)
        {
            _medianBuffer[i] = _frameUs[(start + i) % FrameCapacity];
        }

        Array.Sort(_medianBuffer, 0, count);
        long medianUs = _medianBuffer[count / 2];
        _dynamicSpikeThresholdUs = Math.Max(
            Math.Max(20, PerfSession.FrameSpikeThresholdMs) * 1000L,
            medianUs * 2L);
        _microStutterThresholdUs = Math.Max(medianUs * 5L / 2L, 20_000L);
    }

    private void OnFramePreDraw()
    {
        Volatile.Write(ref _lastPreDrawUsec, (long)Time.GetTicksUsec());
    }

    private void OnFramePostDraw()
    {
        Volatile.Write(ref _lastPostDrawUsec, (long)Time.GetTicksUsec());
    }

    private static string BuildRecentLog()
    {
        try
        {
            string[] tail = PerfTimeline.GetRecentLogTail(3);
            if (tail.Length == 0)
            {
                return "";
            }

            string joined = string.Join(" | ", tail);
            return joined.Length <= 240 ? joined : joined[..240];
        }
        catch
        {
            return "";
        }
    }

    private double HistogramPercentile(int percentile)
    {
        if (_histogramFrames <= 0)
        {
            return 0;
        }

        long target = _histogramFrames * percentile / 100L;
        long running = 0;
        for (int i = 0; i < _histogram.Length; i++)
        {
            running += _histogram[i];
            if (running >= target)
            {
                return i;
            }
        }

        return _histogram.Length - 1;
    }

    private static bool IsWindowFocused()
    {
        try
        {
            return DisplayServer.WindowIsFocused();
        }
        catch
        {
            return true;
        }
    }

    private static long ReadPipelineCompilations()
    {
        try
        {
            return (long)Performance.GetMonitor(Performance.Monitor.PipelineCompilationsCanvas)
                   + (long)Performance.GetMonitor(Performance.Monitor.PipelineCompilationsSurface)
                   + (long)Performance.GetMonitor(Performance.Monitor.PipelineCompilationsDraw)
                   + (long)Performance.GetMonitor(Performance.Monitor.PipelineCompilationsMesh)
                   + (long)Performance.GetMonitor(Performance.Monitor.PipelineCompilationsSpecialization);
        }
        catch
        {
            return 0;
        }
    }

    private static long ReadTextureMem()
    {
        try
        {
            return (long)Performance.GetMonitor(Performance.Monitor.RenderTextureMemUsed);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>读取 CPU setup 毫秒；无数据返回 -1。</summary>
    private static double ReadFrameSetupCpuMs()
    {
        try
        {
            double ms = RenderingServer.GetFrameSetupTimeCpu();
            return double.IsFinite(ms) && ms >= 0 ? ms : -1;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>读取刷新率，失败回退 60Hz。</summary>
    private static double ReadRefreshRateHz()
    {
        try
        {
            double hz = DisplayServer.ScreenGetRefreshRate(DisplayServer.WindowGetCurrentScreen());
            return double.IsFinite(hz) && hz >= 20 ? hz : 60.0;
        }
        catch
        {
            return 60.0;
        }
    }

    /// <summary>将 Present 等待折算为刷新周期数。</summary>
    private static double PresentPeriods(long presentWaitUs, double refreshRateHz)
    {
        if (presentWaitUs <= 0 || !double.IsFinite(refreshRateHz) || refreshRateHz <= 0)
        {
            return 0;
        }

        return presentWaitUs * refreshRateHz / 1_000_000.0;
    }

    /// <summary>仅在 BDS_PERF_GPU_TIMING=1 时测量根视口 GPU，避免默认开销。</summary>
    private void SetupGpuTiming()
    {
        try
        {
            if (!string.Equals(OS.GetEnvironment("BDS_PERF_GPU_TIMING"), "1", StringComparison.Ordinal))
            {
                return;
            }

            Viewport? viewport = GetViewport();
            if (viewport == null)
            {
                return;
            }

            _rootViewportRid = viewport.GetViewportRid();
            RenderingServer.ViewportSetMeasureRenderTime(_rootViewportRid, true);
            _gpuTimingEnabled = true;
            GpuTimingEnabled = true;
        }
        catch
        {
            _gpuTimingEnabled = false;
            GpuTimingEnabled = false;
        }
    }

    /// <summary>读取根视口 GPU 毫秒；未启用或异常返回 0。</summary>
    private static double ReadGpuRenderTimeMs(Rid viewportRid)
    {
        try
        {
            double ms = RenderingServer.ViewportGetMeasuredRenderTimeGpu(viewportRid);
            return double.IsFinite(ms) && ms >= 0 ? ms : 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>将 GPU 时间折算为刷新周期数。</summary>
    private static double GpuPeriods(double gpuMs, double refreshRateHz)
    {
        if (gpuMs <= 0 || !double.IsFinite(gpuMs) || !double.IsFinite(refreshRateHz) || refreshRateHz <= 0)
        {
            return 0;
        }

        return gpuMs * refreshRateHz / 1000.0;
    }

    /// <summary>每秒统计离屏与合成节点数量。</summary>
    private void CountSceneNodes(out int viewports, out int canvasGroups, out int worldEnvironments)
    {
        viewports = 0;
        canvasGroups = 0;
        worldEnvironments = 0;
        try
        {
            Node? root = GetTree()?.Root;
            if (root == null)
            {
                return;
            }

            CountSceneNodesRecursive(root, ref viewports, ref canvasGroups, ref worldEnvironments);
        }
        catch
        {
            // 节点失效时保留已统计数据。
        }
    }

    private static void CountSceneNodesRecursive(
        Node node,
        ref int viewports,
        ref int canvasGroups,
        ref int worldEnvironments)
    {
        if (node is SubViewport)
        {
            viewports++;
        }

        if (node is CanvasGroup)
        {
            canvasGroups++;
        }

        if (node is WorldEnvironment)
        {
            worldEnvironments++;
        }

        int childCount = node.GetChildCount();
        for (int i = 0; i < childCount; i++)
        {
            CountSceneNodesRecursive(node.GetChild(i), ref viewports, ref canvasGroups, ref worldEnvironments);
        }
    }

    private void ResetSecondWindow(long now)
    {
        _secondStartUsec = now;
        _secondFrames = 0;
        _secondTotalUs = 0;
        _secondMaxUs = 0;
        _secondOver33 = 0;
        _secondOver50 = 0;
        _secondOver100 = 0;
        _secondMicroStutters = 0;
        _secondAllocBytes = 0;
        _secondNodesAdded = 0;
        _secondFrameSetupMsSum = 0;
        _secondFrameSetupSamples = 0;
        _secondGpuMsSum = 0;
        _secondGpuSamples = 0;
        _secondGpuMsMax = 0;
    }

    private void MaybeWriteAutoReport(long now)
    {
        long mainMenuAtMs = PerfTimeline.MainMenuAtMs;
        if (mainMenuAtMs >= 0 && _mainMenuUsec < 0)
        {
            // 仅启动初期开启采样时写启动报告。
            if (PerfTimeline.ElapsedMs - mainMenuAtMs <= 120_000)
            {
                _mainMenuUsec = now;
            }
            else
            {
                _startupReported = true;
            }
        }

        if (!_startupReported && _mainMenuUsec >= 0 && now - _mainMenuUsec >= StartupReportDelayUsec)
        {
            _startupReported = true;
            // 主线程快照，后台写盘。
            PerfSession.Dump("startup");
            _lastPeriodicReportUsec = now;
            return;
        }

        long intervalUs = Math.Max(30, PerfSession.PeriodicReportSeconds) * 1_000_000L;
        if (now - _lastPeriodicReportUsec >= intervalUs)
        {
            _lastPeriodicReportUsec = now;
            // 仅出现长帧时主动刷新报告。
            if (HasRecentAnomaly())
            {
                PerfSession.Dump("periodic");
            }
        }
    }

    private bool HasRecentAnomaly()
    {
        int start = Math.Max(0, _seconds.Count - 5);
        int over100 = 0;
        int microStutters = 0;
        double maxMs = 0;
        for (int i = start; i < _seconds.Count; i++)
        {
            SecondSample sample = _seconds[i];
            over100 += sample.Over100;
            microStutters += sample.MicroStutters;
            if (sample.MaxMs > maxMs)
            {
                maxMs = sample.MaxMs;
            }
        }

        double maxAllowed = Math.Max(100, PerfSession.FrameSpikeThresholdMs * 2);
        return over100 > 0 || microStutters >= 5 || maxMs >= maxAllowed;
    }

    private static string SafeSceneName(Node? node)
    {
        try
        {
            return node != null && GodotObject.IsInstanceValid(node) ? node.Name.ToString() : "";
        }
        catch
        {
            return "";
        }
    }
}
