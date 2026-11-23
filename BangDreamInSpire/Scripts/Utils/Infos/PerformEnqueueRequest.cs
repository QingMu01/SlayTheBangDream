using BangDreamInSpire.Scripts.Enums;

namespace BangDreamInSpire.Scripts.Utils.Infos;

/// <summary>本次入场的落位覆盖；空请求使用卡牌自身策略，请求消费后即失效。</summary>
public readonly record struct PerformEnqueueRequest
{
    /// <summary>全局槽位索引（1 基）。</summary>
    public int? SlotIndex { get; private init; }

    public PerformChord? Group { get; private init; }

    public bool IsEmpty => SlotIndex is null && Group is null;

    /// <summary>槽位索引非法时返回空请求。</summary>
    public static PerformEnqueueRequest Slot(int slotIndex)
    {
        return slotIndex >= 1 ? new PerformEnqueueRequest { SlotIndex = slotIndex } : default;
    }

    /// <summary><see cref="PerformChord.None" /> 视为未指定。</summary>
    public static PerformEnqueueRequest ForGroup(PerformChord group)
    {
        return group == PerformChord.None ? default : new PerformEnqueueRequest { Group = group };
    }
}

/// <summary>手动选择的候选槽位（1 基）及其和弦分组。</summary>
public readonly record struct PerformSlotCandidate(int SlotIndex, PerformChord Group);
