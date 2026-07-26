using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ItsCrychic.Scripts.Power.Buff;

public class OperationStrategyPower : BandPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;


    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext,
        ICombatState combatState)
    {
        if (player != Owner.Player) return;

        var cardHistories = CombatManager.Instance.History.CardPlaysFinished
            .Where(entry => entry.CardPlay.Player == Owner.Player && entry.CardPlay.Card.Type == CardType.Skill)
            .Select(entry => entry.CardPlay.Card)
            .ToList();
        if (cardHistories.Count > 0)
        {
            var selectedCard = cardHistories.StableShuffle(player.RunState.Rng.CombatCardSelection)
                .Take(Math.Clamp(Amount, 0, cardHistories.Count))
                .Select(card =>
                {
                    var cardModel = card.CreateClone();
                    cardModel.SetToFreeThisTurn();
                    return cardModel;
                })
                .ToList();

            if (selectedCard.Count > 0)
            {
                Flash();
                await CardPileCmd.Add(selectedCard, PileType.Hand);
            }
        }
    }
}