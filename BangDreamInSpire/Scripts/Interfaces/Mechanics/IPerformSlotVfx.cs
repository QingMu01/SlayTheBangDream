using Godot;

namespace BangDreamInSpire.Scripts.Interfaces.Mechanics;

/// <summary>
/// 演奏反馈特效的最小契约：能被摆进槽位。
/// 演奏区只认这个接口，所以自定义反馈不一定要继承核心的 <c>PerformSealVfx</c>——
/// 继承它可以直接复用「墨带扫过 → 边缘吃光 → 落印压点」这条统一时间线；
/// 自己写一个 <c>Node2D</c> + 本接口也能接进演奏流程。
/// </summary>
public interface IPerformSlotVfx
{
    /// <summary>注入槽位的未缩放尺寸与相对变换；由演奏区在入树前调用。</summary>
    void ConfigureSlot(Vector2 slotSize, Transform2D slotTransform);
}
