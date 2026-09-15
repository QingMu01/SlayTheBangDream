using Godot;

namespace BangDreamLib.Scripts.Mechanics.Perform;

/// <summary>
/// 演奏槽位的通用配色。具体某个槽位取哪一种颜色由角色的演奏方案
/// （<see cref="Interfaces.CharacterAugment.IPerformScheme.GetSlotColor" />）决定。
/// </summary>
public static class PerformSlotColors
{
    /// <summary>空槽位，或槽位内的卡牌不是演奏卡时使用的默认色。</summary>
    public static readonly Color Default = new("#9d9d9d");

    /// <summary>即兴卡的槽位色。</summary>
    public static readonly Color Instant = new("#63a5ff");

    /// <summary>演奏卡的槽位色。</summary>
    public static readonly Color Perform = new("#d30150");
}
