using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Mechanics.Perform;

/// <summary>
/// 「插曲」附魔的机制判定与统一接入点。附魔模型位于内容模组，核心经
/// <see cref="IInsertSongEnchantment" /> 标记接口识别，不产生反向依赖。
/// </summary>
public static class InsertSongRules
{
    /// <summary>该牌是否带有「插曲」附魔。</summary>
    public static bool HasInsertSong(CardModel card)
    {
        return card.Enchantment is IInsertSongEnchantment;
    }

    /// <summary>
    /// 取该牌出牌时的落位选择模式：带插曲附魔时等价于
    /// <see cref="PerformTargetTypes.AnySlot" />（任意已激活槽位），否则按卡牌自身目标类型判定。
    /// </summary>
    public static PerformSlotSelectionMode ResolveSlotSelectionMode(CardModel card)
    {
        return HasInsertSong(card)
            ? PerformSlotSelectionMode.Slot
            : PerformTargetTypes.ResolveMode(card.TargetType);
    }

    /// <summary>该牌出牌时是否需要玩家手动指定落位。</summary>
    public static bool IsManualSlotCard(CardModel card)
    {
        return ResolveSlotSelectionMode(card) != PerformSlotSelectionMode.None;
    }

    /// <summary>
    /// 【曲库】→【歌单】的唯一接入点：带插曲附魔的牌改为进入手牌，其余保持放入歌单。
    /// </summary>
    public static async Task MoveIntoPerformArea(CardModel card)
    {
        if (HasInsertSong(card))
        {
            await CardPileCmd.Add(card, PileType.Hand);
            return;
        }

        await CardPileCmd.Add(card, BangDreamConst.PerformPile);
    }
}
