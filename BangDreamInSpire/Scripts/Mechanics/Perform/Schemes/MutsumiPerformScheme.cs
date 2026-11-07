using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Mechanics.Perform.Schemes;

/// <summary>
/// 睦的演奏方案：C/D/F/G 四个和弦分组，每组容量上限 3、初始 1。
/// 四组分别锚定在角色命中框的左上 / 左下 / 右上 / 右下，槽位颜色由所属和弦分组决定，
/// 不使用槽位提示。
/// 卡牌和弦不参与入组判定（和弦只用于激活奏响）：音乐牌默认进入 C 组，需要进入其他分组时
/// 由卡牌的 <c>PerformTargetTypes.RequestGroup</c> 手动指定；打出带和弦的卡牌时，
/// 其全部和弦分组一起奏响。
/// </summary>
public sealed class MutsumiPerformScheme : IPerformScheme
{
    /// <summary>单组合容量上限。</summary>
    public const int GroupCapacityLimit = 3;

    private static readonly PerformGroupDescriptor[] GroupDescriptors =
    [
        new(PerformChord.C, 1, GroupCapacityLimit, 1, IsDefault: true),
        new(PerformChord.D, 1 + GroupCapacityLimit, GroupCapacityLimit, 1),
        new(PerformChord.F, 1 + GroupCapacityLimit * 2, GroupCapacityLimit, 1),
        new(PerformChord.G, 1 + GroupCapacityLimit * 3, GroupCapacityLimit, 1)
    ];

    /// <summary>和弦分组四角锚点，槽位配色统一取 <see cref="PerformChordColors" />。</summary>
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

    /// <summary>
    /// 睦的槽位颜色固定由所属和弦分组决定，与该槽位内是否有牌无关，
    /// 因此四个角能直接以颜色标示各自的和弦。
    /// </summary>
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
