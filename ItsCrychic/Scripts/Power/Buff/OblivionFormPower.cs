using BangDreamLib.Scripts.Powers;
using BangDreamLib.Scripts.Utils;
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
    private const int Multiplier = 3;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    private bool _shouldMultiply;

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (Owner.Player != null)
        {
            var drawPileCards = BangDreamConst.ExtraDraw.GetPile(Owner.Player).Cards.ToList();
            var cardModel = Owner.Player.RunState.Rng.CombatCardSelection.NextItem(drawPileCards);
            if (cardModel != null)
            {
                await CardCmd.Exhaust(new BlockingPlayerChoiceContext(), cardModel);
                _shouldMultiply = true;
            }
        }
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        _shouldMultiply = false;
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer == Owner && props.IsPoweredAttack() && cardSource is { Type: CardType.Attack } && _shouldMultiply)
        {
            return Multiplier;
        }

        return 1m;
    }
}