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
        var player = Owner.Player;
        if (player == null || cardPlay.Card.Owner != player || cardPlay.Card.Type != CardType.Attack)
        {
            return;
        }

        var drawPileCards = BangDreamConst.ExtraDraw.GetPile(player).Cards.ToList();
        var cardModel = player.RunState.Rng.CombatCardSelection.NextItem(drawPileCards);
        if (cardModel != null)
        {
            await CardCmd.Exhaust(new BlockingPlayerChoiceContext(), cardModel);
            _shouldMultiply = true;
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
        if (dealer != Owner || !props.IsPoweredAttack() || cardSource is not { Type: CardType.Attack })
        {
            return 1m;
        }

        if (cardSource.Pile?.Type == PileType.Play)
        {
            return _shouldMultiply && (cardPlay == null || cardPlay.Card == cardSource)
                ? Multiplier
                : 1m;
        }

        if (cardPlay != null || Owner.Player == null)
        {
            return 1m;
        }

        return BangDreamConst.ExtraDraw.GetPile(Owner.Player).Cards.Count > 0
            ? Multiplier
            : 1m;
    }
}
