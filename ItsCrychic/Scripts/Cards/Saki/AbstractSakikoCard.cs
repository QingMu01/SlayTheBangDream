using BangDreamLib.Scripts.Attributes;
using BangDreamLib.Scripts.Cards;
using ItsCrychic.Scripts.Character.CardPools;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Scaffolding.Content;

namespace ItsCrychic.Scripts.Cards.Saki;

[BangDreamPool(typeof(SakikoStandardCardPool))]
public abstract class AbstractSakikoCard(
    int baseCost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true)
    : BandCardModel(baseCost, type, rarity, target, showInCardLibrary)
{
    protected override CardAssetProfile CardAssetProfile => CrychicConst.DefaultCardAssetProfile(this);
}