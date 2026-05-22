using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ItsCrychic.Scripts.Power.Buff;

public class PuppetTheaterPower : BandPowerModel, ILingeredChangedHook
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    private int _count;

    public async Task AfterLingeredEnergyReduced(Player player, int amount)
    {
        if (player == Owner.Player && _count < Amount)
        {
            _count++;

            var discardPileCards = player.PlayerCombatState?.DiscardPile.Cards;
            if (discardPileCards is { Count: > 0 })
            {
                var randomCard = player.RunState.Rng.CombatCardSelection.NextItem(discardPileCards);
                if (randomCard != null)
                {
                    Flash();
                    await CardPileCmd.Add(randomCard, PileType.Hand);
                }
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