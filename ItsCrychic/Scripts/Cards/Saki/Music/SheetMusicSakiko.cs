using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class SheetMusicSakiko() : AbstractSakikoMusicCard(CustomRarity, CustomTarget), IPerformHookListener
{
    private const CardRarity CustomRarity = CardRarity.Basic;
    private const TargetType CustomTarget = TargetType.None;
    private decimal _grantedAmount;

    public override bool IsInstant => true;

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>()
    ];

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.PerformArea
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Buff.Create(1)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext)
    {
        var amount = QuickVar.Buff.GetVar(this).BaseValue;
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, amount,
            Owner.Creature, this);
        await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, amount,
            Owner.Creature, this);
        _grantedAmount += amount;
    }

    public async Task OnCardLeavePerformArea(PlayerChoiceContext choiceContext, CardModel cardModel)
    {
        if (cardModel != this || _grantedAmount <= 0) return;

        var amount = _grantedAmount;
        _grantedAmount = 0;
        var strengthPower = Owner.Creature.GetPower<StrengthPower>();
        var dexterityPower = Owner.Creature.GetPower<DexterityPower>();
        if (strengthPower != null)
        {
            await PowerCmd.ModifyAmount(choiceContext, strengthPower, -amount, Owner.Creature, this);
        }

        if (dexterityPower != null)
        {
            await PowerCmd.ModifyAmount(choiceContext, dexterityPower, -amount, Owner.Creature, this);
        }
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
