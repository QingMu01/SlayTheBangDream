using Godot;

namespace BangDreamInSpire.Scripts.Interfaces.Mechanics;

/// <summary>演奏反馈特效的最小契约；继承 <c>PerformSealVfx</c> 可复用统一时间线。</summary>
public interface IPerformSlotVfx
{
    /// <summary>注入槽位的未缩放尺寸与相对变换。</summary>
    void ConfigureSlot(Vector2 slotSize, Transform2D slotTransform);
}
