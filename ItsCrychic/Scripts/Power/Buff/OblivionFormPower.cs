using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Powers;
using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Cards.Token;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Power.Buff;

public class OblivionFormPower : BandPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer == Owner && props.IsPoweredAttack() &&
            cardSource is { Type: CardType.Attack } &&
            Owner.Player != null &&
            Owner.Player.AttachedData().PerformManager.PerformPile.Cards.Count == 0)
        {
            return Amount;
        }

        return 1m;
    }

    public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player || cardPlay.Card.Type != CardType.Attack ||
            Owner.Player == null || Owner.CombatState == null) return;

        var manager = Owner.Player.AttachedData().PerformManager;
        while (manager.PerformPile.Cards.Count < manager.Capacity)
        {
            var residue = Owner.CombatState.CreateCard<Residue>(Owner.Player);
            await CardPileCmd.AddGeneratedCardToCombat(residue, BangDreamConst.PerformPile, Owner.Player);
        }
    }
}
