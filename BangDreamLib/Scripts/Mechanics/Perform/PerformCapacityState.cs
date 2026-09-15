using BangDreamLib.Scripts.Enums;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Mechanics.Perform.Chord;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Mechanics.Perform;

/// <summary>
/// 容量变更结果。
/// </summary>
public enum PerformCapacityChangeResult
{
    /// <summary>变更已生效。</summary>
    Applied,

    /// <summary>未发生变更（变化量为零，或指定了方案中不存在的分组）。</summary>
    Ignored,

    /// <summary>目标分组已达容量上限，变更被拒绝。</summary>
    RejectedAtMax
}

/// <summary>
/// 每玩家、每场战斗的演奏容量状态，由 <see cref="IPerformScheme" /> 的分组定义初始化。
/// 战斗开始时整体重建，因此角色级方案不会残留上一场的容量。
/// 全局槽位索引按分组划分固定块：分组 <c>g</c> 的容量为 <c>c</c> 时占用
/// <c>[BaseSlotIndex, BaseSlotIndex + c - 1]</c>，块之间不移动。
/// </summary>
public sealed class PerformCapacityState(IPerformScheme scheme)
{
    private readonly Dictionary<PerformChord, int> _capacityByGroup = scheme.Groups.ToDictionary(
        group => group.Chord,
        group => Math.Clamp(group.DefaultSlots, 0, group.MaxSlots));

    private Dictionary<PerformChord, PerformGroupDescriptor>? _groupByChord;
    private List<int>? _allActiveSlots;

    private Dictionary<PerformChord, PerformGroupDescriptor> GroupByChord =>
        _groupByChord ??= scheme.Groups.ToDictionary(group => group.Chord);

    /// <summary>全部分组的容量上限之和。</summary>
    public int TotalSlotCount => scheme.TotalSlotCount;

    /// <summary>当前已激活的槽位总数。</summary>
    public int ActiveSlotCount => _capacityByGroup.Values.Sum();

    /// <summary>全部已激活的全局槽位索引，升序。</summary>
    public IReadOnlyList<int> ActiveSlots => _allActiveSlots ??= BuildActiveSlots(null);

    /// <summary>取指定分组的当前容量；分组不存在时为 0。</summary>
    public int GetCapacity(PerformChord group)
    {
        return _capacityByGroup.GetValueOrDefault(group);
    }

    /// <summary>取指定分组的容量上限；分组不存在时为 0。</summary>
    public int GetMaxSlots(PerformChord group)
    {
        return GroupByChord.TryGetValue(group, out var descriptor) ? descriptor.MaxSlots : 0;
    }

    /// <summary>判断全局槽位索引当前是否有效（处于某分组块内且未超出该分组容量）。</summary>
    public bool IsValidSlot(int slotIndex)
    {
        return TryResolveSlot(slotIndex, out var descriptor, out var localOrdinal) &&
               localOrdinal < GetCapacity(descriptor.Chord);
    }

    /// <summary>
    /// 取指定掩码覆盖的已激活槽位索引，升序。传入 <see cref="PerformChord.None" /> 时返回全部分组。
    /// </summary>
    public IReadOnlyList<int> GetActiveSlots(PerformChord mask)
    {
        return mask == PerformChord.None ? ActiveSlots : BuildActiveSlots(mask);
    }

    /// <summary>
    /// 解析卡牌所属分组：取卡牌和弦中优先级最高的一个；无和弦的卡牌（含
    /// <see langword="null" />）归入方案的默认分组。
    /// </summary>
    public PerformChord ResolveGroup(CardModel? card)
    {
        if (card != null)
        {
            foreach (var group in PerformChordStore.GetChord(card).EnumerateGroups())
            {
                if (_capacityByGroup.ContainsKey(group))
                {
                    return group;
                }
            }
        }

        return _capacityByGroup.ContainsKey(scheme.DefaultGroup)
            ? scheme.DefaultGroup
            : _capacityByGroup.Keys.FirstOrDefault();
    }

    /// <summary>
    /// 增加容量。未指定分组时对全部分组生效。
    /// </summary>
    public PerformCapacityChangeResult Add(int amount, PerformChord? group = null)
    {
        if (amount <= 0)
        {
            return PerformCapacityChangeResult.Ignored;
        }

        var targets = ResolveTargetGroups(group);
        if (targets.Count == 0)
        {
            return PerformCapacityChangeResult.Ignored;
        }

        return ApplyDelta(amount, targets) ? PerformCapacityChangeResult.Applied : PerformCapacityChangeResult.RejectedAtMax;
    }

    /// <summary>
    /// 减少容量。未指定分组时对全部分组生效。
    /// </summary>
    public bool Reduce(int amount, PerformChord? group = null)
    {
        if (amount <= 0)
        {
            return false;
        }

        return ApplyDelta(-amount, ResolveTargetGroups(group));
    }

    private bool ApplyDelta(int delta, IReadOnlyList<PerformGroupDescriptor> targets)
    {
        var changed = false;
        foreach (var descriptor in targets)
        {
            var current = GetCapacity(descriptor.Chord);
            var next = Math.Clamp(current + delta, 0, descriptor.MaxSlots);
            if (next == current)
            {
                continue;
            }

            _capacityByGroup[descriptor.Chord] = next;
            changed = true;
        }

        if (changed)
        {
            _allActiveSlots = null;
        }

        return changed;
    }

    private IReadOnlyList<PerformGroupDescriptor> ResolveTargetGroups(PerformChord? group)
    {
        if (group == null)
        {
            return scheme.Groups;
        }

        return GroupByChord.TryGetValue(group.Value, out var descriptor) ? [descriptor] : [];
    }

    private List<int> BuildActiveSlots(PerformChord? mask)
    {
        var slots = new List<int>();
        foreach (var descriptor in scheme.Groups)
        {
            if (mask.HasValue && !mask.Value.HasFlag(descriptor.Chord))
            {
                continue;
            }

            var capacity = GetCapacity(descriptor.Chord);
            for (var localOrdinal = 0; localOrdinal < capacity; localOrdinal++)
            {
                slots.Add(descriptor.BaseSlotIndex + localOrdinal);
            }
        }

        return slots;
    }

    private bool TryResolveSlot(int slotIndex, out PerformGroupDescriptor descriptor, out int localOrdinal)
    {
        foreach (var candidate in scheme.Groups)
        {
            if (slotIndex < candidate.BaseSlotIndex || slotIndex >= candidate.BaseSlotIndex + candidate.MaxSlots)
            {
                continue;
            }

            descriptor = candidate;
            localOrdinal = slotIndex - candidate.BaseSlotIndex;
            return true;
        }

        descriptor = default;
        localOrdinal = 0;
        return false;
    }
}
