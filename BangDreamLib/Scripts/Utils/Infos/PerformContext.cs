using BangDreamLib.Scripts.Enums;
using BangDreamLib.Scripts.Mechanics.Perform;
using BangDreamLib.Scripts.Nodes.SubNode;

namespace BangDreamLib.Scripts.Utils.Infos;

public class PerformContext(
    NPerformItem? slot,
    PerformManager? manager,
    int slotIndex = -1,
    PerformEnqueueStrategy strategy = PerformEnqueueStrategy.Nearby,
    int aspirationSlot = -1
)
{
    /// <summary>
    /// 管理者
    /// </summary>
    public PerformManager? Manager { get; set; } = manager;

    /// <summary>
    /// 插槽节点
    /// </summary>
    public NPerformItem? Slot { get; set; } = slot;

    /// <summary>
    /// 插槽索引（全局，1 基）
    /// 取值范围由角色演奏方案的槽位总数决定。
    /// </summary>
    public int SlotIndex { get; set; } = slotIndex;

    /// <summary>
    /// 入队策略
    /// </summary>
    public PerformEnqueueStrategy Strategy { get; } = strategy;

    /// <summary>
    /// 入队期望插槽索引
    /// </summary>
    public int AspirationSlot { get; } = aspirationSlot;

    /// <summary>
    /// 当前演奏是否由休止消耗余音触发。
    /// </summary>
    public bool IsSubsideTriggered { get; set; }
}
