using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;

namespace BangDreamInSpire.Scripts.Mechanics.Perform;

public enum PerformCapacityChangeResult
{
    Applied,

    Ignored,

    RejectedAtMax
}

/// <summary>每玩家每场战斗的容量状态；全局槽位按分组固定块划分，索引不随容量移动。</summary>
public sealed class PerformCapacityState(IPerformScheme scheme)
{
    private readonly Dictionary<PerformChord, int> _capacityByGroup = BuildCapacityByGroup(scheme);

    private Dictionary<PerformChord, PerformGroupDescriptor>? _groupByChord;
    private List<int>? _allActiveSlots;

    private Dictionary<PerformChord, PerformGroupDescriptor> GroupByChord =>
        _groupByChord ??= BuildGroupIndex(scheme);

    public int TotalSlotCount => scheme.TotalSlotCount;

    public int ActiveSlotCount => _capacityByGroup.Values.Sum();

    /// <summary>升序的已激活全局槽位。</summary>
    public IReadOnlyList<int> ActiveSlots => _allActiveSlots ??= BuildActiveSlots(null);

    /// <summary>读取分组容量，未知分组为 0。</summary>
    public int GetCapacity(PerformChord group)
    {
        return _capacityByGroup.GetValueOrDefault(group);
    }

    /// <summary>读取分组容量上限，未知分组为 0。</summary>
    public int GetMaxSlots(PerformChord group)
    {
        return GroupByChord.TryGetValue(group, out var descriptor) ? descriptor.MaxSlots : 0;
    }

    /// <summary>槽位是否落在某分组的激活容量内。</summary>
    public bool IsValidSlot(int slotIndex)
    {
        return TryResolveSlot(slotIndex, out var descriptor, out var localOrdinal) &&
               localOrdinal < GetCapacity(descriptor.Chord);
    }

    /// <summary>返回掩码覆盖的激活槽位，空掩码表示全部。</summary>
    public IReadOnlyList<int> GetActiveSlots(PerformChord mask)
    {
        return mask == PerformChord.None ? ActiveSlots : BuildActiveSlots(mask);
    }

    /// <summary>返回方案默认分组，失效时回退首个分组。</summary>
    public PerformChord ResolveDefaultGroup()
    {
        return _capacityByGroup.ContainsKey(scheme.DefaultGroup)
            ? scheme.DefaultGroup
            : _capacityByGroup.Keys.FirstOrDefault();
    }

    /// <summary>槽位是否属于掩码分组，与当前容量无关。</summary>
    public bool IsSlotInMask(int slotIndex, PerformChord mask)
    {
        return TryResolveSlot(slotIndex, out var descriptor, out _) &&
               (mask == PerformChord.None || mask.HasFlag(descriptor.Chord));
    }

    /// <summary>增加容量，空掩码表示全部分组。</summary>
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

    /// <summary>减少容量，空掩码表示全部分组。</summary>
    public bool Reduce(int amount, PerformChord? group = null)
    {
        if (amount <= 0)
        {
            return false;
        }

        return ApplyDelta(-amount, ResolveTargetGroups(group));
    }

    /// <summary>按和弦索引分组，重复时报告冲突。</summary>
    private static Dictionary<PerformChord, PerformGroupDescriptor> BuildGroupIndex(IPerformScheme scheme)
    {
        var byChord = new Dictionary<PerformChord, PerformGroupDescriptor>();
        foreach (var group in scheme.Groups)
        {
            if (!byChord.TryAdd(group.Chord, group))
            {
                throw new InvalidOperationException(
                    $"演奏方案 {scheme.GetType().Name} 的分组和弦重复：'{group.Chord}'。" +
                    "同一方案内分组和弦必须唯一（PerformChord.None 至多出现在一个分组）。");
            }
        }

        return byChord;
    }

    private static Dictionary<PerformChord, int> BuildCapacityByGroup(IPerformScheme scheme)
    {
        return BuildGroupIndex(scheme).ToDictionary(
            pair => pair.Key,
            pair => Math.Clamp(pair.Value.DefaultSlots, 0, pair.Value.MaxSlots));
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
