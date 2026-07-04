using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Power.Buff;

public class PuppetTheaterPower : BandPowerModel, ISecondaryResourceHookListener
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    private int _count;

    public async Task AfterSecondaryResourceChanged(SecondaryResourceChangeContext context)
    {
        if (context.Player == Owner.Player && context.Reason == SecondaryResourceChangeReason.Spend)
        {
            if (_count < Amount)
            {
                var discardPileCards = Owner.Player.PlayerCombatState?.DiscardPile.Cards;
                if (discardPileCards is { Count: > 0 })
                {
                    var randomCard = Owner.Player.RunState.Rng.CombatCardSelection.NextItem(discardPileCards);
                    if (randomCard != null)
                    {
                        Flash();
                        await CardPileCmd.Add(randomCard, PileType.Hand);
                    }
                }

                _count++;
            }
        }
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner.Player)
        {
            _count = 0;
        }

        return Task.CompletedTask;
    }
}