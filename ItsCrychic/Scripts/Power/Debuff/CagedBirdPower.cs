using BangDreamLib.Scripts.Powers;
using ItsCrychic.Scripts.Cards.Saki.Attack;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ItsCrychic.Scripts.Power.Debuff;

public class CagedBirdPower : BandPowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    public CardModel? SourceCard { get; private set; }

    protected override IEnumerable<IHoverTip> PowerHoverTips =>
    [
        HoverTipFactory.FromCard<CagedBird>()
    ];

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        SourceCard = cardSource;
        await PowerCmd.Apply<WeakPower>(new BlockingPlayerChoiceContext(), Owner, 1, applier, cardSource);
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power,
        decimal amount, Creature? applier, CardModel? cardSource)
    {
        var currentWeak = Owner.GetPower<WeakPower>();
        if (amount >= 0 || power is not WeakPower || power.Owner != Owner || currentWeak is { Amount: > 0 })
            return;

        await PowerCmd.Apply<WeakPower>(choiceContext, Owner, 1, applier, cardSource);
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        var weak = oldOwner.GetPower<WeakPower>();
        if (weak != null)
        {
            await PowerCmd.Decrement(weak);
        }
    }
}