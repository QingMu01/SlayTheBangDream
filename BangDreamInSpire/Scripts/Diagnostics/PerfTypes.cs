using MegaCrit.Sts2.Core.Logging;

namespace BangDreamInSpire.Scripts.Diagnostics;

/// <summary>
/// 启动/阶段时间线上的一个打点。<see cref="AtMs" /> 是相对 mod 开始初始化的毫秒数。
/// </summary>
internal readonly record struct PerfMark(long AtMs, string Label, long DurationMs, string? Detail);

/// <summary>
/// 一条被捕获的游戏日志记录（带相对时间，便于把日志和设备侧现象对齐）。
/// </summary>
internal readonly record struct PerfLogLine(long AtMs, LogLevel Level, string Text);

/// <summary>
/// 一次「缓存未命中、主线程同步加载」的记录。
/// </summary>
internal readonly record struct SyncLoadRecord(string Path, long Usec, long AtMs, bool ModOwned);

/// <summary>
/// 一次资源预加载会话（对应 <c>Preloading '&lt;name&gt;'</c> 的一段）。
/// </summary>
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

/// <summary>
/// 一个长帧快照。<c>Before</c> 系列字段来自上一帧结束时的上下文（更接近「卡住之前」），
/// <c>After</c> 系列是恢复后读到的状态；两者不同说明这一帧跨越了场景/加载状态切换。
/// 计数器字段（节点Δ/shaderΔ/GC0Δ）都是相对上一次 1Hz 采样的增量，不是累计值。
/// </summary>
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
    public int Gc2Delta;
    public double GcPauseMs;
    public string RecentLog = "";
}

/// <summary>
/// 每秒一次的聚合采样，用于观察内存/对象/分配随时间的趋势，避免每帧调用 Performance。
/// </summary>
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
}

/// <summary>阶段维度的帧统计（按 1Hz 采样聚合，开销很低）。</summary>
internal sealed class PhaseFrameStats
{
    public long Frames;
    public double TotalMs;
    public double MaxMs;

    public double AvgMs => Frames == 0 ? 0 : TotalMs / Frames;
}

/// <summary>
/// 帧采样器在写报告时导出的一次性快照。
/// </summary>
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

/// <summary>
/// 一次报告写盘所需的数据（在主线程采集完成，可安全地交给后台线程写文件）。
/// </summary>
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
