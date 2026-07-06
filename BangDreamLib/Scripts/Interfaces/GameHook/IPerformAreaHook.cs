using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Interfaces.GameHook;

public interface IPerformAreaHook
{
    Task OnCardEnterPerformArea(CardModel cardModel) => Task.CompletedTask;

    Task OnCardLeavePerformArea(CardModel cardModel) => Task.CompletedTask;
}