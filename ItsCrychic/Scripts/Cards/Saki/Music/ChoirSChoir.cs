using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class ChoirSChoir() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None)
{
    protected override IEnumerable<CardKeyword> CardKeywords => [BangDreamConst.Perform];

    protected override IEnumerable<DynamicVar> CardVars => [QuickVar.Gold.Create(7)];

    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (Handle != null && dealer == Owner.Creature && result.WasTargetKilled)
        {
            FlashInArea();
            await PlayerCmd.GainGold(DynamicVars.Gold.BaseValue, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Gold.UpgradeValueBy(3);
    }
}