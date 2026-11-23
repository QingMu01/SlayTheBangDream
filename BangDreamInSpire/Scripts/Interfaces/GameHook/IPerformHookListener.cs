using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Interfaces.GameHook;

public interface IPerformHookListener
{
    Task OnCardEnterPerformArea(PlayerChoiceContext choiceContext, CardModel cardModel) => Task.CompletedTask;

    Task OnCardLeavePerformArea(PlayerChoiceContext choiceContext, CardModel cardModel) => Task.CompletedTask;

    Task OnCardPerform(PlayerChoiceContext choiceContext, CardPerform perform) => Task.CompletedTask;
}