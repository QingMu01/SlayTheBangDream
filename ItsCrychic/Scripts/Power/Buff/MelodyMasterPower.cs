using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Power.Buff;

public class MelodyMasterPower : BandPowerModel, ICardPerformanceHook
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    private List<CardModel> _applySlyCards = [];

    public async Task OnCardEnterPerformanceArea(CardModel cardModel)
    {
        await Task.CompletedTask;
    }

    public Task OnCardLeavePerformanceArea(CardModel cardModel)
    {
        if (cardModel is not IPerformanceCard && cardModel.Owner == Owner.Player)
        {
            Flash();
            cardModel.AddKeyword(CardKeyword.Sly);
            _applySlyCards.Add(cardModel);
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (_applySlyCards.Contains(play.Card))
        {
            _applySlyCards.Remove(play.Card);
        }

        return Task.CompletedTask;
    }
}