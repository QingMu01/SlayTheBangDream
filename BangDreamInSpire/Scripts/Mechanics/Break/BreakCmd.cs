using BangDreamInSpire.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Mechanics.Break;

/// <summary>
/// 破绽的唯一施加入口。<b>卡牌/遗物/能力发现破绽时必须走这里</b>，
/// 才能保证：层数截断、满层进入弱点击破、Hook 广播与常驻视觉刷新。
/// </summary>
public static class BreakCmd
{
    /// <summary>目标当前是否可以再被发现破绽（存活、未处于弱点击破、且未满层）。</summary>
    public static bool CanDiscover(Creature? target)
    {
        if (target == null || target.IsDead || target.CombatState == null)
        {
            return false;
        }

        return target.GetPower<WeakPointBreakPower>() == null &&
               target.GetPowerAmount<WeaknessBreakPower>() < BreakConst.MaxStacks;
    }

    /// <summary>
    /// 对 <paramref name="target" /> 发现 <paramref name="amount" /> 个破绽。
    /// </summary>
    /// <param name="choiceContext">发起结算的玩家选择上下文。</param>
    /// <param name="target">目标（任意单位）。</param>
    /// <param name="amount">期望发现的层数；会被截断到上限。</param>
    /// <param name="source">
    /// 发起模型。若是 <see cref="CardModel" /> 会作为 <c>cardSource</c> 传入能力施加流程，
    /// 这样「击破」命中集合能把这次发现计入该牌；<see cref="PowerModel" /> / 遗物等则只推导施加者。
    /// </param>
    /// <returns>本次实际生效的层数（被完全阻挡时返回 0）。</returns>
    public static async Task<int> Discover(PlayerChoiceContext choiceContext, Creature target, int amount,
        AbstractModel? source)
    {
        if (target == null || amount <= 0 || target.IsDead)
        {
            return 0;
        }

        var combatState = target.CombatState;
        if (combatState == null || target.GetPower<WeakPointBreakPower>() != null)
        {
            return 0;
        }

        var (applier, cardSource) = ResolveSource(source);
        var before = target.GetPowerAmount<WeaknessBreakPower>();
        if (before >= BreakConst.MaxStacks)
        {
            // 自愈：已经满层却没有弱点击破状态（理论上只有能力施加被打断才会出现）——补上状态。
            await TryEnterWeakPointBreak(choiceContext, combatState, target, applier, cardSource, source);
            return 0;
        }

        var room = BreakConst.MaxStacks - before;
        var apply = Math.Min(amount, room);

        // silent：破绽没有 Power 图标，非 silent 会让目标出现一次「无来由的受击抖动」与 debuff 音效，
        // 反馈交给常驻 VFX。
        await PowerCmd.Apply<WeaknessBreakPower>(choiceContext, target, apply, applier, cardSource, silent: true);

        var applied = target.GetPowerAmount<WeaknessBreakPower>() - before;
        if (applied <= 0)
        {
            return 0;
        }

        NWeaknessBreakVfx.Sync(target);
        await BangDreamHook.AfterWeaknessBreakDiscovered(choiceContext, combatState, target, applied, source);

        await TryEnterWeakPointBreak(choiceContext, combatState, target, applier, cardSource, source);
        return applied;
    }

    private static async Task TryEnterWeakPointBreak(
        PlayerChoiceContext choiceContext,
        ICombatState combatState,
        Creature target,
        Creature? applier,
        CardModel? cardSource,
        AbstractModel? source)
    {
        if (target.GetPowerAmount<WeaknessBreakPower>() < BreakConst.MaxStacks)
        {
            return;
        }

        if (target.GetPower<WeakPointBreakPower>() != null || target.IsDead)
        {
            return;
        }

        await PowerCmd.Apply<WeakPointBreakPower>(choiceContext, target, BreakConst.BreakDurationTurns, applier,
            cardSource, silent: true);

        if (target.GetPower<WeakPointBreakPower>() == null)
        {
            return;
        }

        NWeaknessBreakVfx.Sync(target);
        await BangDreamHook.AfterWeakPointBroken(choiceContext, combatState, target, source);
    }

    /// <summary>从发起模型推导「施加者」与「卡牌来源」，两者都会进入原版能力施加流程。</summary>
    private static (Creature? Applier, CardModel? CardSource) ResolveSource(AbstractModel? source)
    {
        return source switch
        {
            CardModel card => (card.Owner?.Creature, card),
            PowerModel power => (power.Owner, null),
            RelicModel relic => (relic.Owner?.Creature, null),
            _ => (null, null)
        };
    }
}
