using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Interfaces.GameHook;

/// <summary>歌单进出流程通过本接口让角色规则决定是否演奏新进入的牌。</summary>
public interface IPerformTriggerListener
{
    /// <summary>在歌单进出结算完成后回调；无玩家选择的重排重进入等场景传入空上下文。</summary>
    Task OnCardTriggeredPerform(PlayerChoiceContext? choiceContext, CardModel cardModel) => Task.CompletedTask;
}
