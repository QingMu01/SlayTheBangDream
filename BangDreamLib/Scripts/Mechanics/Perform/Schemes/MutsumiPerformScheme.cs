using BangDreamLib.Scripts.Enums;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Mechanics.Perform.Schemes;

/// <summary>
/// 睦的演奏方案：C/D/F/G 四个和弦分组，每组容量上限 3、初始 1。
/// 四组分别锚定在角色命中框的左上 / 左下 / 右上 / 右下，槽位颜色由所属和弦分组决定，
/// 不使用槽位提示。无和弦的卡牌归入 C 组；多和弦卡牌按 C→D→F→G 的优先级入组，
/// 奏响时覆盖其全部和弦分组。
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

    private static readonly Dictionary<PerformChord, PerformSlotCorner> GroupCorners = new()
    {
        [PerformChord.C] = PerformSlotCorner.TopLeft,
        [PerformChord.D] = PerformSlotCorner.BottomLeft,
        [PerformChord.F] = PerformSlotCorner.TopRight,
        [PerformChord.G] = PerformSlotCorner.BottomRight
    };

    /// <summary>和弦分组配色，与槽位所属和弦种类一一对应。</summary>
    private static readonly Dictionary<PerformChord, Color> GroupColors = new()
    {
        [PerformChord.C] = new("#7799CC"),
        [PerformChord.D] = new("#779977"),
        [PerformChord.F] = new("#FFDD88"),
        [PerformChord.G] = new("#FF8899")
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
        return descriptor != null && GroupColors.TryGetValue(descriptor.Value.Chord, out var color)
            ? color
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
