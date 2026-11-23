using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Utils.Infos;
using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Character.PerformSchemes;

/// <summary>单分组演奏方案；槽位沿命中框底边向上排列，提示显示余音数。</summary>
public sealed class SakikoPerformScheme : IPerformScheme
{
    public const int DefaultCapacity = 3;

    public const int CapacityLimit = 7;

    private static readonly PerformGroupDescriptor[] GroupDescriptors =
    [
        new(PerformChord.None, 1, CapacityLimit, DefaultCapacity, IsDefault: true)
    ];

    /// <inheritdoc />
    public IReadOnlyList<PerformGroupDescriptor> Groups => GroupDescriptors;

    /// <inheritdoc />
    public PerformChord DefaultGroup => PerformChord.None;

    /// <inheritdoc />
    public int TotalSlotCount => CapacityLimit;

    /// <inheritdoc />
    public PerformAreaPlacement AreaPlacement => new(PerformAreaAnchor.LeftOfHitbox, 50f);

    /// <inheritdoc />
    public bool UsesSlotHint => true;

    /// <inheritdoc />
    public PerformSlotAnchor GetSlotAnchor(int slotIndex)
    {
        return slotIndex is < 1 or > CapacityLimit
            ? new PerformSlotAnchor(PerformSlotCorner.BottomCenter, 0)
            : new PerformSlotAnchor(PerformSlotCorner.BottomCenter, slotIndex - 1);
    }

    /// <inheritdoc />
    public Color GetSlotColor(int slotIndex, CardModel? card)
    {
        return card is IPerformCard performCard
            ? performCard.IsInstant
                ? PerformSlotColors.Instant
                : PerformSlotColors.Perform
            : PerformSlotColors.Default;
    }

    /// <inheritdoc />
    public bool TryGetHintAmount(Player player, out int amount)
    {
        amount = SecondaryResourceCmd.Get(player, BangDreamConst.LingeredResource);
        return true;
    }
}
