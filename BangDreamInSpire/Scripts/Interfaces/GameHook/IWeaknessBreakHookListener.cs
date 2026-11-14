using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Interfaces.GameHook;

/// <summary>
/// 破绽 / 弱点击破的三个广播点。实现者通常是战斗内可被订阅的模型
/// （能力、遗物、卡牌、机制单例等），通过 <c>BangDreamHook</c> 统一派发。
/// </summary>
public interface IWeaknessBreakHookListener
{
    /// <summary>任意单位实际获得破绽层数后广播（含 1 层；被阻挡/已满层时不会广播）。</summary>
    /// <param name="choiceContext">发起结算的玩家选择上下文。</param>
    /// <param name="target">获得破绽的单位。</param>
    /// <param name="amount">本次实际增加的层数。</param>
    /// <param name="source">`发现`的发起模型（通常是卡牌，也可能是遗物/能力）。</param>
    Task AfterWeaknessBreakDiscovered(PlayerChoiceContext? choiceContext, Creature target, int amount,
        AbstractModel? source)
    {
        return Task.CompletedTask;
    }

    /// <summary>目标破绽累计到上限、进入[gold]弱点击破[/gold]状态时广播。</summary>
    Task AfterWeakPointBroken(PlayerChoiceContext? choiceContext, Creature target, AbstractModel? source)
    {
        return Task.CompletedTask;
    }

    /// <summary>一张带击破效果的牌命中了处于弱点击破的目标，且该目标的击破效果已经结算完毕后广播。</summary>
    Task AfterWeakPointBreakTriggered(PlayerChoiceContext? choiceContext, Creature target, CardPlay play)
    {
        return Task.CompletedTask;
    }
}
