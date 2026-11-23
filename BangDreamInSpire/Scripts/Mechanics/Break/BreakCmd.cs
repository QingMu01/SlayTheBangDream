using BangDreamInSpire.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Mechanics.Break;

/// <summary>破绽统一施加入口，负责截断、状态转换、广播与视觉刷新。</summary>
public static class BreakCmd
{
    /// <summary>目标是否可继续施加破绽。</summary>
    public static bool CanDiscover(Creature? target)
    {
        if (target == null || target.IsDead || target.CombatState == null)
        {
            return false;
        }

        return target.GetPower<WeakPointBreakPower>() == null &&
               target.GetPowerAmount<WeaknessBreakPower>() < BreakConst.MaxStacks;
    }

    /// <summary>对目标施加破绽并返回实际层数；CardModel 会作为 cardSource 记入命中集合。</summary>
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
            // 满层但状态缺失时补齐弱点击破。
            await TryEnterWeakPointBreak(choiceContext, combatState, target, applier, cardSource, source);
            return 0;
        }

        var room = BreakConst.MaxStacks - before;
        var apply = Math.Min(amount, room);

        // 禁用默认受击反馈，由常驻 VFX 表达。
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

    /// <summary>推导施加者与卡牌来源。</summary>
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
