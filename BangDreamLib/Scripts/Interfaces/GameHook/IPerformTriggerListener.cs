using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Interfaces.GameHook;

/// <summary>
/// 演奏触发监听者。歌单进出流程在牌进入歌单时回调本接口，由角色规则决定是否演奏该牌
/// （祥子的即兴演奏即由此驱动）。
/// </summary>
public interface IPerformTriggerListener
{
    /// <summary>
    /// 有牌进入歌单、且歌单进出结算完成时点。
    /// </summary>
    /// <param name="choiceContext">
    /// 触发本次进出的上下文；重排重进入等无玩家选择的场景为 <see langword="null" />。
    /// </param>
    /// <param name="cardModel">进入歌单的卡牌。</param>
    Task OnCardTriggeredPerform(PlayerChoiceContext? choiceContext, CardModel cardModel) => Task.CompletedTask;
}
