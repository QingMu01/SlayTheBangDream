using MegaCrit.Sts2.Core.Logging;

namespace BangDreamInSpire.Scripts.Diagnostics;

internal readonly record struct PerfMark(long AtMs, string Label, long DurationMs, string? Detail);

internal readonly record struct PerfLogLine(long AtMs, LogLevel Level, string Text);

/// <summary>缓存未命中导致的主线程同步加载记录。</summary>
internal readonly record struct SyncLoadRecord(string Path, long Usec, long AtMs, bool ModOwned);

internal sealed class PreloadSessionRecord
{
    public string Name = "";
    public int Requested;
    public int Injected;
    public long StartMs;
    public long EndMs = -1;
    public long LogWallMs = -1;
    public long MainThreadUsec;
    public long MaxProcessUsec;
    public int ProcessCalls;
    public bool Completed;

    /// <summary>游戏日志里报告的墙钟耗时优先；没有时用我们自己观测的起止差。</summary>
    public long WallMs => LogWallMs >= 0 ? LogWallMs : EndMs >= 0 ? EndMs - StartMs : -1;

    public double MainThreadMs => MainThreadUsec / 1000.0;

    public double MaxProcessMs => MaxProcessUsec / 1000.0;
}

/// <summary>Before/After 分别取自长帧前后，可识别跨场景或加载切换；Delta 字段相对上一次 1Hz 采样，而非累计值。</summary>
internal sealed class FrameSpike
{
    public long AtMs;
    public double Ms;
    public string PhaseBefore = "";
    public string PhaseAfter = "";
    public string SceneBefore = "";
    public string SceneAfter = "";
    public string LoadingBefore = "";
    public string LoadingAfter = "";
    public long Nodes;
    public long NodeDelta;
    public long DrawCalls;
    public long ShaderCompileDelta;
    public int PipelineDelta;
    public long TextureMemBytes;
    public long TextureMemDelta;
    public int Gc0Delta;
    public long AllocBytes;
    public long ManagedBytes;
    public long RenderSubmitUs;
    public long PresentWaitUs;

    /// <summary>GetFrameSetupTimeCpu 的毫秒值换算为微秒；不可用、负值或 NaN 归 0。</summary>
    public long FrameSetupCpuUs;

    /// <summary>Present 等待的刷新周期数；大于 2 通常指向 GPU 队列、合成器或驱动阻塞，而非帧逻辑。</summary>
    public double PresentWaitedPeriods;

    /// <summary>根视口 GPU 渲染毫秒数；仅 BDS_PERF_GPU_TIMING=1 时采集，无效值归 0。</summary>
    public double GpuRenderTimeMs;

    /// <summary>GPU 时间的刷新周期数；与 <see cref="PresentWaitedPeriods" /> 对比可区分 GPU 饱和与合成器/驱动等待。</summary>
    public double GpuPeriods;

    /// <summary>上一秒 1Hz 采样时统计到的 <c>SubViewport</c> 数量（渲染路径复杂度的代理）。</summary>
    public int ViewportNodes;

    /// <summary>上一秒 1Hz 采样时统计到的 <c>CanvasGroup</c> 数量（额外 draw call / 离屏合成的常见来源）。</summary>
    public int CanvasGroupNodes;

    public int Gc2Delta;
    public double GcPauseMs;
    public string RecentLog = "";
}

/// <summary>每秒聚合采样，避免逐帧调用 Performance。</summary>
internal sealed class SecondSample
{
    public long AtMs;
    public int Frames;
    public double MaxMs;
    public double AvgMs;
    public double Fps;
    public string Phase = "";
    public long Nodes;
    public long OrphanNodes;
    public long DrawCalls;
    public long StaticMemBytes;
    public long VideoMemBytes;
    public long AllocBytes;
    public int NodesAdded;
    public int ShaderCompiles;
    public int PipelineDelta;
    public long TextureMemBytes;
    public long TextureMemDelta;
    public int Over33;
    public int Over50;
    public int Over100;
    public int MicroStutters;
    public double GcPauseMs;
    public int Gc2Delta;

    public double FrameSetupCpuMsAvg;

    /// <summary>仅启用 BDS_PERF_GPU_TIMING=1 时非 0。</summary>
    public double GpuRenderTimeMsAvg;

    public double GpuRenderTimeMsMax;

    public int ViewportNodes;

    public int CanvasGroupNodes;

    public int WorldEnvironmentNodes;
}

internal sealed class PhaseFrameStats
{
    public long Frames;
    public double TotalMs;
    public double MaxMs;

    public double AvgMs => Frames == 0 ? 0 : TotalMs / Frames;
}

internal sealed class PerfFrameSnapshot
{
    public long TotalFrames;
    public int WindowFrames;
    public long BackgroundFrames;
    public long BackgroundMs;
    public long SpikeCount;
    public double MaxMs;
    public double AvgMs;
    public double P50Ms;
    public double P95Ms;
    public double P99Ms;
    public List<FrameSpike> Spikes = new();
    public List<SecondSample> Seconds = new();
    public Dictionary<string, PhaseFrameStats> PhaseStats = new();
}

/// <summary>主线程采集完成后可交给后台线程写盘的报告数据。</summary>
internal sealed class PerfReportPayload
{
    public string Reason = "";
    public string Directory = "";
    public string MarkdownPath = "";
    public string JsonPath = "";
    public string Markdown = "";
    public string Json = "";
    public int RetainReports;
    public long ElapsedMs;
    public long TotalFrames;
    public double P95Ms;
    public double MaxMs;
    public long SpikeCount;
    public int PreloadSessions;
    public long SyncLoadCount;
}
