namespace BangDreamInSpire.Scripts.Diagnostics;

/// <summary>设置仅在 mod 初始化时读取，改动需重启；关闭时诊断模块不加载。</summary>
internal sealed class PerfConfig
{
    /// <summary>设置页绑定与初始化读取必须共用此 key。</summary>
    public const string DataKey = "perf_settings";

    public bool Enabled { get; set; }
}
