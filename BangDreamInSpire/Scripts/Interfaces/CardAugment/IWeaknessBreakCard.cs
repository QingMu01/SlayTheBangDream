using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace BangDreamInSpire.Scripts.Interfaces.CardAugment;

/// <summary>
/// 带「击破」效果的卡牌。
/// <para>
/// 卡牌文案约定使用「击破：……」句式，由卡牌自身实现 <c>ICardDescriptionContributor</c> 渲染。
/// 触发规则由 <c>BangDreamInSpire.Scripts.Mechanics.Break.BreakRules</c> 统一裁决：
/// 这张牌被打出后，只要**命中的目标**里存在处于[gold]弱点击破[/gold]状态的单位，
/// 就对每个这样的目标调用一次 <see cref="OnWeakPointBreak" />（同一张牌对同一目标只触发一次）。
/// </para>
/// </summary>
public interface IWeaknessBreakCard
{
    /// <summary>这张牌是否带「击破」效果；没有则不参与击破触发。</summary>
    bool HasBreakEffect { get; }

    /// <summary>
    /// 击破效果本体。在 <c>AfterCardPlayedLate</c> 时结算，早于此时点的「发现破绽」已经全部结算完毕，
    /// 因此「本张牌凑满 4 层破绽 → 进入弱点击破」也能立刻吃到击破。
    /// </summary>
    /// <param name="choiceContext">当前出牌的玩家选择上下文。</param>
    /// <param name="play">本次出牌。</param>
    /// <param name="target">处于弱点击破的目标（每次调用一个）。</param>
    Task OnWeakPointBreak(PlayerChoiceContext choiceContext, CardPlay play, Creature target);
}
