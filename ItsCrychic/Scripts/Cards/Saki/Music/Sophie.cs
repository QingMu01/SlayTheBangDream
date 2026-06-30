using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Cards.Saki.Attack;
using ItsCrychic.Scripts.Cards.Saki.Skill;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class Sophie() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None)
{
    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromCard(ModelDb.Card<StrikeSakiko>().ToMutable()),
        HoverTipFactory.FromCard(ModelDb.Card<DefendSakiko>().ToMutable())
    ];

    protected override IEnumerable<CardKeyword> CardKeywords => [BangDreamConst.Performance];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        ModCardVars.Int("StrikeDamage", 3),
        ModCardVars.Int("DefendBlock", 2)
    ];

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
    {
        return Handle != null && dealer == Owner.Creature && cardSource?.Tags.Contains(CardTag.Strike) == true
            ? DynamicVars["StrikeDamage"].BaseValue
            : 0;
    }

    public override decimal ModifyBlockAdditive(Creature target, decimal amount, ValueProp props, CardModel? cardSource,
        CardPlay? cardPlay)
    {
        return Handle != null && target == Owner.Creature && cardSource?.Tags.Contains(CardTag.Defend) == true
            ? DynamicVars["DefendBlock"].BaseValue
            : 0;
    }

    protected override void OnUpgrade()
    {
        DynamicVars["StrikeDamage"].UpgradeValueBy(1);
        DynamicVars["DefendBlock"].UpgradeValueBy(1);
    }
}