using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Power.Buff;

public class OperationStrategyPower : BandPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;


    public override async Task AfterAutoPrePlayPhaseEntered(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player && !CombatManager.Instance.History.CardPlaysFinished.Any()) return;
        var waitAddCards = new List<CardModel>();
        while (waitAddCards.Count < Amount)
        {
            var randomCard = player.RunState.Rng.CombatCardSelection
                .NextItem(CombatManager.Instance.History.CardPlaysFinished)?.CardPlay.Card;
            if (randomCard != null)
            {
                var cloneCard = randomCard.CreateClone();
                cloneCard.SetToFreeThisTurn();
                waitAddCards.Add(cloneCard);
            }
            else
            {
                break;
            }
        }

        if (waitAddCards.Count > 0)
        {
            await CardPileCmd.Add(waitAddCards, PileType.Hand);
        }
    }
}