using BangDreamInSpire.Scripts.Enums;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Interfaces.CharacterAugment;

/// <summary>槽位锚点方位。</summary>
public enum PerformSlotCorner
{
    /// <summary>命中框底边向上竖排，并逐格靠近玩家。</summary>
    BottomCenter,

    TopLeft,

    BottomLeft,

    TopRight,

    BottomRight
}

/// <summary>槽位锚点；Offset 为组内 0 基序号，像素换算由 NPerformArea 负责。</summary>
public readonly record struct PerformSlotAnchor(PerformSlotCorner Corner, int Offset);

/// <summary>演奏区摆放基准。</summary>
public enum PerformAreaAnchor
{
    LeftOfHitbox,

    HitboxCenter
}

/// <summary>演奏区摆放规则；HorizontalMargin 仅用于 LeftOfHitbox。</summary>
public readonly record struct PerformAreaPlacement(PerformAreaAnchor Anchor, float HorizontalMargin);

/// <summary>共享容量的槽位分组。BaseSlotIndex 为 1 基全局起始索引，
/// IsDefault 表示未指定时的入组。</summary>
public readonly record struct PerformGroupDescriptor(
    PerformChord Chord,
    int BaseSlotIndex,
    int MaxSlots,
    int DefaultSlots,
    bool IsDefault = false);

/// <summary>不可变演奏策略；战斗可变容量存于 PerformCapacityState，避免多玩家共享污染。</summary>
public interface IPerformScheme
{
    /// <summary>按 BaseSlotIndex 排序的分组；Chord 必须唯一，否则容量初始化失败。</summary>
    IReadOnlyList<PerformGroupDescriptor> Groups { get; }

    /// <summary>未指定分组时的默认入组。</summary>
    PerformChord DefaultGroup { get; }

    /// <summary>全局槽位索引上界。</summary>
    int TotalSlotCount { get; }

    PerformAreaPlacement AreaPlacement { get; }

    /// <summary>是否启用槽位提示及二级资源订阅。</summary>
    bool UsesSlotHint { get; }

    /// <summary>返回槽位锚点，越界回退 BottomCenter。</summary>
    PerformSlotAnchor GetSlotAnchor(int slotIndex);

    /// <summary>返回槽位颜色；card 为空槽时为 null。</summary>
    Color GetSlotColor(int slotIndex, CardModel? card);

    /// <summary>读取槽位提示，不启用时返回 false。</summary>
    bool TryGetHintAmount(Player player, out int amount);
}
