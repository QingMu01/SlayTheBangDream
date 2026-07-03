using BangDreamLib.Scripts.Cards;
using ItsCrychic.Scripts.Character.CardPools;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ItsCrychic.Scripts.Cards.Mutsumi;

[RegisterCard(typeof(MutsumiMusicalCardPool), Inherit = true)]
public abstract class AbstractMutsumiMusicCard(int baseCost, CardRarity rarity, TargetType target)
    : MusicCardModel(baseCost, rarity, target)
{
    protected override CardAssetProfile CardAssetProfile => CrychicConst.DefaultCardAssetProfile(this);
}