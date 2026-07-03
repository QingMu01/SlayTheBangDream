using BangDreamLib.Scripts.Cards;
using ItsCrychic.Scripts.Character.CardPools;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ItsCrychic.Scripts.Cards.Saki;

[RegisterCard(typeof(SakikoMusicalCardPool), Inherit = true)]
public abstract class AbstractSakikoMusicCard(int baseCost, CardRarity rarity, TargetType target)
    : MusicCardModel(baseCost, rarity, target)
{
    protected AbstractSakikoMusicCard(CardRarity rarity, TargetType target) : this(0, rarity, target)
    {
    }

    protected override CardAssetProfile CardAssetProfile => CrychicConst.DefaultCardAssetProfile(this);
}