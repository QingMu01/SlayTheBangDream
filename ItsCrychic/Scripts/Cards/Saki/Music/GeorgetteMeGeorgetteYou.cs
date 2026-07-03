using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class GeorgetteMeGeorgetteYou() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None)
{
    protected override IEnumerable<CardKeyword> CardKeywords => [BangDreamConst.Perform];

    protected override IEnumerable<DynamicVar> CardVars => [ModCardVars.Int("DamagePerPower", 4)];

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        if (Handle == null || target == null || dealer != Owner.Creature || cardSource?.Owner != Owner ||
            props.HasFlag(ValueProp.Move))
        {
            return 0;
        }

        return target.Powers.Count * DynamicVars["DamagePerPower"].BaseValue;
    }

    protected override void OnUpgrade()
    {
        DynamicVars["DamagePerPower"].UpgradeValueBy(3);
    }
}