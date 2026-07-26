using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Utils;

public static class BangDreamHook
{
    public static decimal CaptureMusicNoteDamageAdditive(
        ICombatState combatState,
        Creature? dealer,
        AbstractModel? source)
    {
        return IterateCombatHookListeners(combatState)
            .OfType<IMusicNoteModifyHookListener>()
            .ToList()
            .Sum(listener => listener.CaptureMusicNoteDamageAdditive(dealer, source));
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
            damageAmount = listeners.Aggregate(damageAmount,
                (current, listener) =>
                    current + listener.ModifyMusicNoteDamageAdditive(target, current, dealer, source));
        }

        if (modifyDamageHookType.HasFlag(ModifyDamageHookType.Multiplicative))
        {
            damageAmount = listeners.Aggregate(damageAmount,
                (current, listener) =>
                    current * listener.ModifyMusicNoteDamageMultiplicative(target, current, dealer, source));
        }

        return damageAmount;
    }

    public static decimal ModifyMusicNoteShotCount(ICombatState combatState, Creature? dealer, decimal amount,
        AbstractModel? source)
    {
        return IterateCombatHookListeners(combatState).OfType<IMusicNoteModifyHookListener>().Aggregate(amount,
            (current, model) => model.ModifyMusicNoteShotCount(current, dealer, source));
    }

    public static decimal ModifyMusicNoteBounceCount(ICombatState combatState, Creature? dealer, decimal amount,
        AbstractModel? source)
    {
        return IterateCombatHookListeners(combatState).OfType<IMusicNoteModifyHookListener>().Aggregate(amount,
            (current, model) => model.ModifyMusicNoteBounceCount(current, dealer, source));
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
        CardModel cardModel)
    {
        await DispatchCombatHooks<IPerformHookListener>(
            choiceContext,
            combatState,
            listener => listener.OnCardEnterPerformArea(choiceContext, cardModel));
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

    public static async Task OnCardPerform(
        ICombatState combatState,
        PerformContext performContext,
        CardModel cardModel)
    {
        await RunPerformHookAction(
            combatState,
            cardModel,
            choiceContext => DispatchCombatHooks<IPerformHookListener>(
                choiceContext,
                combatState,
                listener => listener.OnCardPerform(choiceContext, performContext, cardModel)));
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
                await ExecuteTaskThenInvokeExecutionFinished(
                    model,
                    dispatch((TListener)(object)model));
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
        foreach (var model in listenerModels)
        {
            await ExecuteTaskThenInvokeExecutionFinished(
                model,
                ((IMusicNoteShotHookListener)model).AfterShot(context, dealer));
        }
    }

    public static async Task RunPerformHookAction(
        ICombatState combatState,
        AbstractModel source,
        Func<PlayerChoiceContext, Task> hook)
    {
        if (!CanDispatchCombatHooks())
        {
            return;
        }

        var netId = LocalContext.NetId;
        if (!netId.HasValue)
        {
            return;
        }

        var choiceContext = new HookPlayerChoiceContext(source, netId.Value, combatState, GameActionType.Combat);
        var task = ExecuteTaskThenInvokeExecutionFinished(source, hook(choiceContext));
        await choiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
        await choiceContext.WaitForCompletion();
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

    private static bool CanDispatchCombatHooks()
    {
        return CombatManager.Instance is not { IsOverOrEnding: true, IsStarting: false };
    }

    private static async Task ExecuteTaskThenInvokeExecutionFinished(AbstractModel model, Task task)
    {
        await task;
        model.InvokeExecutionFinished();
    }
}
