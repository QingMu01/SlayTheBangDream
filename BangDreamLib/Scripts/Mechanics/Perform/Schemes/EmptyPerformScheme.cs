using BangDreamLib.Scripts.Enums;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Mechanics.Perform.Schemes;

/// <summary>
/// 空方案：不提供任何演奏容量。用于未实现 <see cref="IPerformableCharacter" /> 的角色，
/// 使演奏机制在这些角色上保持"容量为 0"的既有语义。
/// </summary>
public sealed class EmptyPerformScheme : IPerformScheme
{
    /// <summary>共享实例。</summary>
    public static readonly EmptyPerformScheme Instance = new();

    private EmptyPerformScheme()
    {
    }

    /// <inheritdoc />
    public IReadOnlyList<PerformGroupDescriptor> Groups => [];

    /// <inheritdoc />
    public PerformChord DefaultGroup => PerformChord.None;

    /// <inheritdoc />
    public int TotalSlotCount => 0;

    /// <inheritdoc />
    public PerformAreaPlacement AreaPlacement => new(PerformAreaAnchor.LeftOfHitbox, 50f);

    /// <inheritdoc />
    public bool UsesSlotHint => false;

    /// <inheritdoc />
    public PerformSlotAnchor GetSlotAnchor(int slotIndex)
    {
        return new PerformSlotAnchor(PerformSlotCorner.BottomCenter, 0);
    }

    /// <inheritdoc />
    public Color GetSlotColor(int slotIndex, CardModel? card)
    {
        return PerformSlotColors.Default;
    }

    /// <inheritdoc />
    public bool TryGetHintAmount(Player player, out int amount)
    {
        amount = 0;
        return false;
    }
}
