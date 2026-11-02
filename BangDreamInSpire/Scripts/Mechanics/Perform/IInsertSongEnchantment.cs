namespace BangDreamInSpire.Scripts.Mechanics.Perform;

/// <summary>
/// 「插曲」附魔的标记接口：由附魔模型实现，核心机制只依赖该接口，不依赖具体模组实现。
/// <list type="bullet">
/// <item>附魔牌从【曲库】移入【歌单】时改为进入手牌（见 <see cref="InsertSongRules.MoveIntoPerformArea" />）；</item>
/// <item>附魔牌打出时由玩家自选歌单槽位（等价于 <c>PerformTargetTypes.AnySlot</c>，见
/// <see cref="InsertSongRules.ResolveSlotSelectionMode" />）。</item>
/// </list>
/// </summary>
public interface IInsertSongEnchantment;
