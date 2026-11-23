using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Mechanics.Perform;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace BangDreamInSpire.Scripts.Enchantments;

/// <summary>插曲：音乐牌从曲库进入歌单时改为进手牌，打出可任选槽位。</summary>
[RegisterEnchantment]
public sealed class InsertSong : EnchantmentModel, IInsertSongEnchantment
{
    /// <summary>是否可附魔到音乐牌。</summary>
    public override bool CanEnchant(CardModel card)
    {
        return base.CanEnchant(card) && card is IPerformCard;
    }
}
