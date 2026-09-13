using BangDreamLib.Scripts.Utils;
using Godot;

namespace BangDreamLib.Scripts.Nodes.VFX;

/// <summary>
/// 五线谱光环背景的纯资源场景。本身无逐帧逻辑，仅负责在 F6 单独运行预览时把自身摆到视口中心，
/// 便于设计时观察。作为子场景内嵌进 music_wave / music_equalizer 时不做任何事。
/// </summary>
[Tool]
public partial class StaffRingVfx : Node2D
{
    public override void _Ready()
    {
        if (VfxPreviewSupport.IsPreviewRun(this))
            VfxPreviewSupport.CenterForPreview(this);
    }
}
