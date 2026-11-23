using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Interfaces.Mechanics;

namespace BangDreamInSpire.Scripts.Utils.Infos;

public class PerformContext(
    IPerformSlot? slot,
    IPerformService? manager,
    int slotIndex = -1,
    PerformEnqueueStrategy strategy = PerformEnqueueStrategy.Nearby,
    int aspirationSlot = -1
)
{
    public IPerformService? Manager { get; set; } = manager;

    public IPerformSlot? Slot { get; set; } = slot;

    /// <summary>全局 1 基槽位索引。</summary>
    public int SlotIndex { get; set; } = slotIndex;

    public PerformEnqueueStrategy Strategy { get; } = strategy;

    public int AspirationSlot { get; } = aspirationSlot;

    /// <summary>是否由休止触发。</summary>
    public bool IsSubsideTriggered { get; set; }

    /// <summary>本地手动落位覆盖，随入队网络动作下发，消费后清空。</summary>
    public PerformEnqueueRequest Request { get; set; }
}
