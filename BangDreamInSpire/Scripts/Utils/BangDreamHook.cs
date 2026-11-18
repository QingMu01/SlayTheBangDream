using BangDreamInSpire.Scripts.Interfaces.GameHook;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Utils;

public static class BangDreamHook
{
    private static bool _warnedPerformHookSkipped;

    public static decimal CaptureMusicNoteDamageAdditive(
        ICombatState combatState,
        Creature? dealer,
        AbstractModel? source)
    {
        var total = 0m;
        foreach (var listener in IterateCombatHookListeners(combatState).OfType<IMusicNoteModifyHookListener>())
        {
            try
            {
                total += listener.CaptureMusicNoteDamageAdditive(dealer, source);
            }
            catch (Exception ex)
            {
                WarnListenerFailure(listener, ex);
            }
        }

        return total;
    }

    public static decimal ModifyMusicNoteDamage(
        ICombatState combatState,
        Creature? target,
        Creature? dealer,
        decimal damage,
        AbstractModel? source,
        ModifyDamageHookType modifyDamageHookType
    )
    {
        var damageAmount = damage;
        var listeners = IterateCombatHookListeners(combatState)
            .OfType<IMusicNoteModifyHookListener>()
            .ToList();
        if (modifyDamageHookType.HasFlag(ModifyDamageHookType.Additive))
        {
            foreach (var listener in listeners)
            {
                try
                {
                    damageAmount += listener.ModifyMusicNoteDamageAdditive(target, damageAmount, dealer, source);
                }
                catch (Exception ex)
                {
                    WarnListenerFailure(listener, ex);
                }
            }
        }

        if (modifyDamageHookType.HasFlag(ModifyDamageHookType.Multiplicative))
        {
            foreach (var listener in listeners)
            {
                try
                {
                    damageAmount *= listener.ModifyMusicNoteDamageMultiplicative(target, damageAmount, dealer, source);
                }
                catch (Exception ex)
                {
                    WarnListenerFailure(listener, ex);
                }
            }
        }

        return damageAmount;
    }

    public static decimal ModifyMusicNoteShotCount(ICombatState combatState, Creature? dealer, decimal amount,
        AbstractModel? source)
    {
        foreach (var model in IterateCombatHookListeners(combatState).OfType<IMusicNoteModifyHookListener>())
        {
            try
            {
                amount = model.ModifyMusicNoteShotCount(amount, dealer, source);
            }
            catch (Exception ex)
            {
                WarnListenerFailure(model, ex);
            }
        }

        return amount;
    }

    public static decimal ModifyMusicNoteBounceCount(ICombatState combatState, Creature? dealer, decimal amount,
        AbstractModel? source)
    {
        foreach (var model in IterateCombatHookListeners(combatState).OfType<IMusicNoteModifyHookListener>())
        {
            try
            {
                amount = model.ModifyMusicNoteBounceCount(amount, dealer, source);
            }
            catch (Exception ex)
            {
                WarnListenerFailure(model, ex);
            }
        }

        return amount;
    }

    public static async Task AfterCardSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Card.CombatState);

        await DispatchCombatHooks<ISubsideHookListener>(
            choiceContext,
            play.Card.CombatState,
            listener => listener.AfterCardSubside(choiceContext, play));
    }

    public static async Task OnCardEnterPerformArea(
        PlayerChoiceContext choiceContext,
        ICombatState combatState,
        CardModel cardModel,
        AbstractModel? additionalListenerModel = null)
    {
        await DispatchCombatHooks<IPerformHookListener>(
            choiceContext,
            combatState,
            listener => listener.OnCardEnterPerformArea(choiceContext, cardModel),
            additionalListenerModel);
    }

    public static async Task OnCardLeavePerformArea(
        PlayerChoiceContext choiceContext,
        ICombatState combatState,
        CardModel cardModel)
    {
        await DispatchCombatHooks<IPerformHookListener>(
            choiceContext,
            combatState,
            listener => listener.OnCardLeavePerformArea(choiceContext, cardModel),
            cardModel);
    }

    /// <summary>
    /// 有卡牌进入歌单、且进出结算完成时点。由角色规则（<see cref="IPerformTriggerListener" />）
    /// 决定是否演奏该牌。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="DispatchCombatHooks{TListener}" /> 不同，本方法不把监听者压入
    /// <paramref name="choiceContext" /> 的模型栈，以免改变远端展示的
    /// <see cref="PlayerChoiceContext.LastInvolvedModel" />。
    /// </remarks>
    public static async Task OnCardTriggeredPerform(
        PlayerChoiceContext? choiceContext,
        ICombatState combatState,
        CardModel cardModel)
    {
        var listeners = IterateCombatHookListeners(combatState)
            .Where(model => model is IPerformTriggerListener)
            .ToList();
        Func<IPerformTriggerListener, Task> dispatch =
            listener => listener.OnCardTriggeredPerform(choiceContext, cardModel);
        foreach (var model in listeners)
        {
            await ExecuteTaskThenInvokeExecutionFinished(model, (IPerformTriggerListener)model, dispatch);
        }
    }

    public static async Task OnCardPerform(
        ICombatState combatState,
        CardPerform perform)
    {
        await RunPerformHookAction(
            combatState,
            perform.Card,
            choiceContext => DispatchCombatHooks<IPerformHookListener>(
                choiceContext,
                combatState,
                listener => listener.OnCardPerform(choiceContext, perform)));
    }

    public static Task OnCardPerform(
        PlayerChoiceContext choiceContext,
        ICombatState combatState,
        CardPerform perform)
    {
        return DispatchCombatHooks<IPerformHookListener>(
            choiceContext,
            combatState,
            listener => listener.OnCardPerform(choiceContext, perform));
    }

    private static async Task DispatchCombatHooks<TListener>(
        PlayerChoiceContext choiceContext,
        ICombatState combatState,
        Func<TListener, Task> dispatch,
        AbstractModel? additionalListenerModel = null)
        where TListener : class
    {
        var listenerModels = IterateCombatHookListeners(combatState)
            .Where(model => model is TListener)
            .ToList();
        if (additionalListenerModel is TListener &&
            CanDispatchCombatHooks() &&
            listenerModels.All(model => !ReferenceEquals(model, additionalListenerModel)))
        {
            listenerModels.Add(additionalListenerModel);
        }

        foreach (var model in listenerModels)
        {
            choiceContext.PushModel(model);
            try
            {
                await ExecuteTaskThenInvokeExecutionFinished(model, (TListener)(object)model, dispatch);
            }
            finally
            {
                choiceContext.PopModel(model);
            }
        }
    }

    public static async Task AfterMusicNoteShot(ICombatState combatState, VfxContext context, Player dealer)
    {
        var listenerModels = IterateCombatHookListeners(combatState)
            .Where(model => model is IMusicNoteShotHookListener)
            .ToList();
        Func<IMusicNoteShotHookListener, Task> dispatch = listener => listener.AfterShot(context, dealer);
        foreach (var model in listenerModels)
        {
            await ExecuteTaskThenInvokeExecutionFinished(model, (IMusicNoteShotHookListener)model, dispatch);
        }
    }

    /// <summary>
    /// 任意单位实际获得破绽层数后广播。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="DispatchCombatHooks{TListener}" /> 不同，本方法不把监听者压入
    /// <paramref name="choiceContext" /> 的模型栈（破绽广播只是通知，不需要远端展示模型），
    /// 写法与 <see cref="OnCardTriggeredPerform" /> 一致。
    /// </remarks>
    public static async Task AfterWeaknessBreakDiscovered(
        PlayerChoiceContext? choiceContext,
        ICombatState combatState,
        Creature target,
        int amount,
        AbstractModel? source)
    {
        await DispatchWeaknessBreakHooks(
            combatState,
            listener => listener.AfterWeaknessBreakDiscovered(choiceContext, target, amount, source));
    }

    /// <summary>目标破绽满层、进入弱点击破时广播。</summary>
    public static async Task AfterWeakPointBroken(
        PlayerChoiceContext? choiceContext,
        ICombatState combatState,
        Creature target,
        AbstractModel? source)
    {
        await DispatchWeaknessBreakHooks(
            combatState,
            listener => listener.AfterWeakPointBroken(choiceContext, target, source));
    }

    /// <summary>带击破效果的牌命中弱点击破目标、击破效果结算完毕后广播。</summary>
    public static async Task AfterWeakPointBreakTriggered(
        PlayerChoiceContext? choiceContext,
        ICombatState combatState,
        Creature target,
        CardPlay play)
    {
        await DispatchWeaknessBreakHooks(
            combatState,
            listener => listener.AfterWeakPointBreakTriggered(choiceContext, target, play));
    }

    private static async Task DispatchWeaknessBreakHooks(
        ICombatState combatState,
        Func<IWeaknessBreakHookListener, Task> dispatch)
    {
        foreach (var model in IterateCombatHookListeners(combatState)
                     .Where(model => model is IWeaknessBreakHookListener)
                     .ToList())
        {
            await ExecuteTaskThenInvokeExecutionFinished(model, (IWeaknessBreakHookListener)model, dispatch);
        }
    }

    public static async Task RunPerformHookAction(
        ICombatState combatState,
        AbstractModel source,
        Func<PlayerChoiceContext, Task> hook)
    {
        if (!CanDispatchCombatHooks())
        {
            // 该分支只代表「战斗已结束 / 正在结束」——结束流程中再跑演奏区变更会二次结算，
            // 因此保持不执行，只记一次告警（A-11）。
            WarnPerformHookSkippedOnce(source);
            return;
        }

        var netId = LocalContext.NetId;
        if (!netId.HasValue)
        {
            // 无本地 NetId 时构造不出 HookPlayerChoiceContext，但时机本身仍安全，不能整段跳过（A-11）：
            // 用不绑定玩家的 BlockingPlayerChoiceContext 兜底执行。该上下文不产生 GameAction、
            // 不暂停玩家选择（信号直接完成），语义即「仅执行一次」；HandlePerformAreaChange 及其内部的
            // OnCardEnterPerformArea / OnCardTriggeredPerform 都只把上下文用于模型栈与选择信号，可直接接受。
            var fallbackContext = new BlockingPlayerChoiceContext();
            fallbackContext.PushModel(source);
            try
            {
                await ExecuteTaskThenInvokeExecutionFinished(source, fallbackContext, hook);
            }
            finally
            {
                fallbackContext.PopModel(source);
            }
            return;
        }

        var choiceContext = new HookPlayerChoiceContext(source, netId.Value, combatState, GameActionType.Combat);
        var task = ExecuteTaskThenInvokeExecutionFinished(source, choiceContext, hook);
        await choiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
        await choiceContext.WaitForCompletion();
    }

    public static async Task RunPerformHookAction(
        PlayerChoiceContext choiceContext,
        AbstractModel source,
        Func<PlayerChoiceContext, Task> hook)
    {
        if (!CanDispatchCombatHooks())
        {
            // 与 ICombatState 重载同口径：战斗结束 / 正在结束时保持不执行，只记一次告警。
            WarnPerformHookSkippedOnce(source);
            return;
        }

        choiceContext.PushModel(source);
        try
        {
            await ExecuteTaskThenInvokeExecutionFinished(source, choiceContext, hook);
        }
        finally
        {
            choiceContext.PopModel(source);
        }
    }

    private static IEnumerable<AbstractModel> IterateCombatHookListeners(ICombatState combatState)
    {
        if (!CanDispatchCombatHooks())
        {
            yield break;
        }

        foreach (var model in combatState.IterateHookListeners())
        {
            yield return model;
        }
    }

    public static bool CanDispatchCombatHooks()
    {
        return CombatManager.Instance is not { IsOverOrEnding: true, IsStarting: false };
    }

    /// <summary>
    /// 调用单个监听者并做异常隔离：异常只记 Warn 并跳过该监听者，后续监听者继续分发；
    /// 无论成败都补发 <see cref="AbstractModel.InvokeExecutionFinished" />，避免一个第三方监听者抛错
    /// 让「后续监听者」与「执行完成通知」一起丢失。
    /// </summary>
    private static async Task ExecuteTaskThenInvokeExecutionFinished<TState>(
        AbstractModel model,
        TState state,
        Func<TState, Task> action)
    {
        try
        {
            await action(state);
        }
        catch (Exception ex)
        {
            WarnListenerFailure(model, ex);
        }
        finally
        {
            InvokeExecutionFinishedSafely(model);
        }
    }

    private static void InvokeExecutionFinishedSafely(AbstractModel model)
    {
        try
        {
            model.InvokeExecutionFinished();
        }
        catch (Exception ex)
        {
            WarnListenerFailure(model, ex);
        }
    }

    private static void WarnPerformHookSkippedOnce(AbstractModel source)
    {
        if (_warnedPerformHookSkipped) return;
        _warnedPerformHookSkipped = true;
        BangDreamInSpireCore.Logger.Warn(
            $"[BDS-Hook] 战斗已结束或正在结束，跳过 {source.GetType().Name} 的演奏区 hook（仅告警一次）。");
    }

    private static void WarnListenerFailure(object listener, Exception ex)
    {
        BangDreamInSpireCore.Logger.Warn(
            $"[BDS-Hook] 钩子监听者 {listener.GetType().Name} 抛出异常，已跳过该监听者并继续后续分发: {ex}");
    }
}