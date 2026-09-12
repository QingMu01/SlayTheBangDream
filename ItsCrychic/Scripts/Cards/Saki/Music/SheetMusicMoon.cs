using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Utils.Infos;
using ItsCrychic.Scripts.Power.Temporary;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class SheetMusicMoon() : AbstractSakikoMusicCard(CardRarity.Basic, TargetType.None), IPerformHookListener
{
    private decimal _grantedAmount;

    public override bool IsInstant => true;

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromPower<DexterityPower>()
    ];

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Buff.Create(1)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        var amount = QuickVar.Buff.GetVar(this).BaseValue;
        await PowerCmd.Apply<SheetMusicMoonPower>(choiceContext, Owner.Creature, amount,
            Owner.Creature, this);
        _grantedAmount += amount;
    }

    public async Task OnCardLeavePerformArea(PlayerChoiceContext choiceContext, CardModel cardModel)
    {
        if (cardModel != this || _grantedAmount <= 0) return;

        var temporaryPower = Owner.Creature.GetPower<SheetMusicMoonPower>();
        if (temporaryPower != null)
        {
            await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, _grantedAmount,
                Owner.Creature, this);
            await PowerCmd.ModifyAmount(choiceContext, temporaryPower, -_grantedAmount,
                Owner.Creature, this);
        }

        _grantedAmount = 0;
    }

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (card == this)
        {
            _grantedAmount = 0;
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        QuickVar.Buff.GetVar(this).UpgradeValueBy(1);
    }
}