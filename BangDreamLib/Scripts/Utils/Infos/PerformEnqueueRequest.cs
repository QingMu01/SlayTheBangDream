using BangDreamLib.Scripts.Enums;

namespace BangDreamLib.Scripts.Utils.Infos;

/// <summary>
/// 一次入队的落位覆盖：玩家在出牌时手动指定的槽位或分组。
/// 空请求表示按卡牌自身的入队策略处理；请求只影响本次入场，消费后即失效。
/// </summary>
public readonly record struct PerformEnqueueRequest
{
    /// <summary>玩家指定的全局槽位索引（1 基）；未指定时为 <see langword="null" />。</summary>
    public int? SlotIndex { get; private init; }

    /// <summary>玩家指定的和弦分组；未指定时为 <see langword="null" />。</summary>
    public PerformChord? Group { get; private init; }

    /// <summary>是否没有任何落位覆盖。</summary>
    public bool IsEmpty => SlotIndex is null && Group is null;

    /// <summary>构造"指定任意槽位"请求；槽位索引非法时返回空请求。</summary>
    public static PerformEnqueueRequest Slot(int slotIndex)
    {
        return slotIndex >= 1 ? new PerformEnqueueRequest { SlotIndex = slotIndex } : default;
    }

    /// <summary>构造"指定分组"请求；<see cref="PerformChord.None" /> 视为未指定。</summary>
    public static PerformEnqueueRequest ForGroup(PerformChord group)
    {
        return group == PerformChord.None ? default : new PerformEnqueueRequest { Group = group };
    }
}

/// <summary>手动选择时的一个候选槽位及其所属和弦分组。</summary>
/// <param name="SlotIndex">全局槽位索引（1 基）。</param>
/// <param name="Group">该槽位所属的和弦分组。</param>
public readonly record struct PerformSlotCandidate(int SlotIndex, PerformChord Group);
