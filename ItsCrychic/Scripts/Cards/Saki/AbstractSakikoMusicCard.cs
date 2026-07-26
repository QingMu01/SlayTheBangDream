using BangDreamLib.Scripts.Cards;
using ItsCrychic.Scripts.Character.CardPools;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Interop.AutoRegistration;

namespace ItsCrychic.Scripts.Cards.Saki;

[RegisterCard(typeof(SakikoMusicalCardPool), Inherit = true)]
public abstract class AbstractSakikoMusicCard(int baseCost, CardRarity rarity, TargetType target)
    : MusicCardModel(baseCost, rarity, target)
{
    protected AbstractSakikoMusicCard(CardRarity rarity, TargetType target) : this(0, rarity, target)
    {
    }
}