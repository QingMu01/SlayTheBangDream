using BangDreamLib.Scripts.Enums;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;

namespace BangDreamLib.Scripts.Mechanics.Perform.Schemes;

/// <summary>
/// 祥子的演奏方案：单一分组、容量上限 7、初始 3。
/// 槽位自命中框底边向上竖排、并逐格朝玩家靠近；槽位提示显示当前余音数量。
/// </summary>
public sealed class SakikoPerformScheme : IPerformScheme
{
    /// <summary>默认容量。</summary>
    public const int DefaultCapacity = 3;

    /// <summary>容量上限。</summary>
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

    /// <summary>
    /// 祥子按卡牌性质配色：即兴卡与演奏卡各一色，空槽位或非演奏卡用默认色。
    /// </summary>
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
