using BangDreamLib.Scripts.Powers;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ItsCrychic.Scripts.Power.Buff;

public class CelestialRotationPower : BandPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        return player != Owner.Player ? count : count + Amount;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner.Player)
        {
            var handCards = PileType.Hand.GetPile(Owner.Player).Cards;
            if (handCards.Count > 0)
            {
                var selectedCards = await CardSelectCmd.FromHand(choiceContext, Owner.Player,
                    CardSelectorPrompt.ToPerformance.GetFixedPrefs(Amount),
                    _ => true, this);

                foreach (var selectedCard in selectedCards)
                {
                    await CardPileCmd.Add(selectedCard, BangDreamConst.PerformanceTable);
                }
            }
        }
    }
}