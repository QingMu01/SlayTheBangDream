using Godot;

namespace BangDreamInSpire.Scripts.Nodes.VFX;

/// <summary>
/// 演奏印迹的单条"墨"：一笔带方向的残影。
/// 由 <see cref="PerformSealVfx"/> 按场景里的基准摆放驱动，自身不播动画，便于在编辑器里摆位。
/// </summary>
[Tool]
public partial class PerformSealStreak : Sprite2D
{
    /// <summary>相对印框起拍的出发时刻（秒）。</summary>
    [Export(PropertyHint.Range, "0,0.6,0.005")]
    public float StartTime { get; set; } = 0.10f;

    /// <summary>整笔的存在时间（秒）。</summary>
    [Export(PropertyHint.Range, "0.05,0.6,0.005")]
    public float LifeTime { get; set; } = 0.30f;

    /// <summary>相对基准摆放的位移终点；方向即元素的"墨"流向。</summary>
    [Export]
    public Vector2 EndOffset { get; set; } = new(0f, -34f);

    /// <summary>终点相对基准的缩放倍率，用于做"越飞越散"。</summary>
    [Export(PropertyHint.Range, "0.1,4,0.01")]
    public float EndScaleMultiplier { get; set; } = 1.6f;

    /// <summary>整笔期间的自转角度，飞行轨迹不因此偏转，只转笔尖。</summary>
    [Export(PropertyHint.Range, "-180,180,1")]
    public float SpinDegrees { get; set; }

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float PeakAlpha { get; set; } = 0.9f;

    /// <summary>起势时间：从 0 到 <see cref="PeakAlpha"/> 的上升时长。</summary>
    [Export(PropertyHint.Range, "0.01,0.2,0.005")]
    public float RiseTime { get; set; } = 0.05f;

    /// <summary>留空则用印框给的本体色与高光色混出笔色。</summary>
    [Export]
    public Color Tint { get; set; } = Colors.Transparent;

    internal bool HasOwnTint => Tint.A > 0f;
}
