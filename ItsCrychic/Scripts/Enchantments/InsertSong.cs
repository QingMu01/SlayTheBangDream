using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Mechanics.Perform;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace ItsCrychic.Scripts.Enchantments;

/// <summary>
/// 插曲：只能给音乐牌附魔。
/// <list type="bullet">
/// <item>该牌从【曲库】移入【歌单】时，改为进入手牌（<see cref="InsertSongRules.MoveIntoPerformArea" />）；</item>
/// <item>打出该牌时可自由选择歌单槽位（等价于任意槽位，<see cref="InsertSongRules.ResolveSlotSelectionMode" />）。</item>
/// </list>
/// </summary>
[RegisterEnchantment]
public sealed class InsertSong : EnchantmentModel, IInsertSongEnchantment
{
    /// <summary>限定只能给音乐牌（演奏牌）附魔。</summary>
    public override bool CanEnchant(CardModel card)
    {
        return base.CanEnchant(card) && card is IPerformCard;
    }
}
