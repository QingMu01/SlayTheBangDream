using BangDreamLib.Scripts.Cards;
using ItsCrychic.Scripts.Character.CardPools;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Interop.AutoRegistration;

namespace ItsCrychic.Scripts.Cards.Mutsumi;

[RegisterCard(typeof(MutsumiMusicalCardPool), Inherit = true)]
public abstract class AbstractMutsumiCard(
    int baseCost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true)
    : BandCardModel(baseCost, type, rarity, target, showInCardLibrary)
{
}