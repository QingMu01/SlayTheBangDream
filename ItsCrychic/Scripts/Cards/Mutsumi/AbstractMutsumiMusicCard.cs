using BangDreamLib.Scripts.Cards;
using ItsCrychic.Scripts.Character.CardPools;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Interop.AutoRegistration;

namespace ItsCrychic.Scripts.Cards.Mutsumi;

[RegisterCard(typeof(MutsumiMusicalCardPool), Inherit = true)]
public abstract class AbstractMutsumiMusicCard(int baseCost, CardRarity rarity, TargetType target)
    : MusicCardModel(baseCost, rarity, target)
{
    protected AbstractMutsumiMusicCard(CardRarity rarity, TargetType target) : this(0, rarity, target)
    {
    }
}