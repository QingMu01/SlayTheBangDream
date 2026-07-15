using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Interfaces.GameHook;

public interface IPerformHookListener
{
    /// <summary>
    /// 有卡牌进入歌单时点
    /// </summary>
    Task OnCardEnterPerformArea(PlayerChoiceContext choiceContext, CardModel cardModel) => Task.CompletedTask;

    /// <summary>
    /// 有卡牌离开歌单时点
    /// </summary>
    Task OnCardLeavePerformArea(PlayerChoiceContext choiceContext, CardModel cardModel) => Task.CompletedTask;

    /// <summary>
    /// 有卡牌被演奏时点
    /// </summary>
    Task OnCardPerform(PlayerChoiceContext choiceContext, PerformContext ctx, CardModel cardModel) => Task.CompletedTask;
}