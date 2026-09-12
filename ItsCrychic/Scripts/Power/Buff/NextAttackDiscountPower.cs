using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Power.Buff;

public class NextAttackDiscountPower : BandPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card.Owner.Creature != Owner || card.Type != CardType.Attack ||
            card.Pile?.Type is not (PileType.Hand or PileType.Play))
        {
            return false;
        }

        modifiedCost = Math.Max(0, originalCost - 1);
        return true;
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature == Owner && cardPlay.Card is
                { Type: CardType.Attack, Pile.Type: PileType.Hand or PileType.Play })
        {
            await PowerCmd.Decrement(this);
        }
    }
}