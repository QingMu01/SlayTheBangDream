using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Power.Buff;

public class MelodyMasterPower : BandPowerModel, IPerformAreaHook
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    private readonly List<CardModel> _applySlyCards = [];
    
    public Task OnCardLeavePerformArea(CardModel cardModel)
    {
        if (cardModel is not IPerformCard && cardModel.Owner == Owner.Player)
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