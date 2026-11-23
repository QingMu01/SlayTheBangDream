using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Character.PerformSchemes;

/// <summary>四角和弦演奏方案，无槽位提示。音乐牌默认进入 C 组，
/// 其他分组由 RequestGroup 指定；卡牌和弦只决定奏响的分组，不影响入组。</summary>
public sealed class MutsumiPerformScheme : IPerformScheme
{
    public const int GroupCapacityLimit = 3;

    private static readonly PerformGroupDescriptor[] GroupDescriptors =
    [
        new(PerformChord.C, 1, GroupCapacityLimit, 1, IsDefault: true),
        new(PerformChord.D, 1 + GroupCapacityLimit, GroupCapacityLimit, 1),
        new(PerformChord.F, 1 + GroupCapacityLimit * 2, GroupCapacityLimit, 1),
        new(PerformChord.G, 1 + GroupCapacityLimit * 3, GroupCapacityLimit, 1)
    ];

    /// <summary>四角锚点与和弦配色。</summary>
    private static readonly Dictionary<PerformChord, PerformSlotCorner> GroupCorners = new()
    {
        [PerformChord.C] = PerformSlotCorner.TopLeft,
        [PerformChord.D] = PerformSlotCorner.BottomLeft,
        [PerformChord.F] = PerformSlotCorner.TopRight,
        [PerformChord.G] = PerformSlotCorner.BottomRight
    };

    /// <inheritdoc />
    public IReadOnlyList<PerformGroupDescriptor> Groups => GroupDescriptors;

    /// <inheritdoc />
    public PerformChord DefaultGroup => PerformChord.C;

    /// <inheritdoc />
    public int TotalSlotCount => GroupCapacityLimit * GroupDescriptors.Length;

    /// <inheritdoc />
    public PerformAreaPlacement AreaPlacement => new(PerformAreaAnchor.HitboxCenter, 0f);

    /// <inheritdoc />
    public bool UsesSlotHint => false;

    /// <inheritdoc />
    public PerformSlotAnchor GetSlotAnchor(int slotIndex)
    {
        var descriptor = FindDescriptor(slotIndex);
        if (descriptor == null)
        {
            return new PerformSlotAnchor(PerformSlotCorner.TopLeft, 0);
        }

        var group = descriptor.Value;
        return new PerformSlotAnchor(GroupCorners[group.Chord], slotIndex - group.BaseSlotIndex);
    }

    /// <inheritdoc />
    public Color GetSlotColor(int slotIndex, CardModel? card)
    {
        var descriptor = FindDescriptor(slotIndex);
        return descriptor != null
            ? PerformChordColors.ColorOf(descriptor.Value.Chord)
            : PerformSlotColors.Default;
    }

    /// <inheritdoc />
    public bool TryGetHintAmount(Player player, out int amount)
    {
        amount = 0;
        return false;
    }

    private static PerformGroupDescriptor? FindDescriptor(int slotIndex)
    {
        foreach (var descriptor in GroupDescriptors)
        {
            if (slotIndex >= descriptor.BaseSlotIndex &&
                slotIndex < descriptor.BaseSlotIndex + descriptor.MaxSlots)
            {
                return descriptor;
            }
        }

        return null;
    }
}
