using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Interfaces.GameHook;

public interface ICardPerformanceHook
{
    Task OnCardEnterPerformanceArea(CardModel cardModel);

    Task OnCardLeavePerformanceArea(CardModel cardModel);
}