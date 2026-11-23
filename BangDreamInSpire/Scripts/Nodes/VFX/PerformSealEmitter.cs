using Godot;

namespace BangDreamInSpire.Scripts.Nodes.VFX;

/// <summary>
/// 演奏印迹的粒团：挂在第 <see cref="BeatIndex"/> 拍上发射一次。
/// 粒子材质全部在场景里调（方向、重力、扩散、缩放曲线），脚本只负责"哪一拍响"。
/// </summary>
[Tool]
public partial class PerformSealEmitter : GpuParticles2D
{
    /// <summary>对应印框节拍数组的下标；越界按最后一拍处理。</summary>
    [Export(PropertyHint.Range, "0,7,1")]
    public int BeatIndex { get; set; }

    /// <summary>留空则用印框给的辅色染粒团。</summary>
    [Export]
    public Color Tint { get; set; } = Colors.Transparent;

    internal bool HasOwnTint => Tint.A > 0f;
}
