namespace BangDreamInSpire.Scripts.Diagnostics;

/// <summary>
/// BDS-Perf 的持久化设置。通过 RitsuLib 的 Global 数据槽保存，并在 RitsuLib 设置页提供一个开关。
/// 该值只在 mod 初始化时读取一次，因此改动<b>需要重启游戏生效</b>——关闭时整个诊断模块完全不加载。
/// </summary>
internal sealed class PerfConfig
{
    /// <summary>数据槽 key，设置页绑定与初始化读取必须使用同一个 key。</summary>
    public const string DataKey = "perf_settings";

    /// <summary>默认关闭：只有玩家反馈卡顿时才在设置页打开。</summary>
    public bool Enabled { get; set; }
}
