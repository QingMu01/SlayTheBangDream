using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Power.Buff;

public class ReorganizationPower : BandPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card.Owner != Owner.Player || play.Card.Type != CardType.Attack) return;

        Flash();
        await ExtraPileCmd.Draw(choiceContext, Amount, Owner.Player);
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || dealer == Owner || result.UnblockedDamage <= 0 || !props.IsPoweredAttack()) return;

        var player = Owner.Player;
        if (player == null) return;

        for (var i = 0; i < Amount; i++)
        {
            var cards = player.AttachedData().PerformManager.PerformPile.Cards;
            if (cards.Count == 0) break;

            Flash();
            var card = player.RunState.Rng.CombatCardSelection.NextItem(cards);
            if (card == null) break;
            await CardPileCmd.Add(card, PileType.Discard);
        }
    }
}
