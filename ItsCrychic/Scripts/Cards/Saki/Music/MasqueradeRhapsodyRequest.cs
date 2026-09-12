using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class MasqueradeRhapsodyRequest() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None)
{
    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromPower<PlatingPower>()
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Buff.Create(2)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        await PowerCmd.Apply<PlatingPower>(choiceContext, Owner.Creature,
            QuickVar.Buff.GetVar(this).BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        QuickVar.Buff.GetVar(this).UpgradeValueBy(1m);
    }
}