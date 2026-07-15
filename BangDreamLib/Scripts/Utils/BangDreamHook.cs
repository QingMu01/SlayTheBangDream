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
        if (modifyDamageHookType.HasFlag(ModifyDamageHookType.Additive))
        {
            damageAmount = combatState.IterateHookListeners().OfType<IMusicNoteModifyHookListener>()
                .Aggregate(damageAmount,
                    (current, modifyHook) =>
                        current + modifyHook.ModifyMusicNoteDamageAdditive(target, current, dealer, source));
        }

        if (modifyDamageHookType.HasFlag(ModifyDamageHookType.Multiplicative))
        {
            damageAmount = combatState.IterateHookListeners().OfType<IMusicNoteModifyHookListener>()
                .Aggregate(damageAmount,
                    (current, modifyHook) =>
                        current * modifyHook.ModifyMusicNoteDamageMultiplicative(target, current, dealer, source));
        }

        return damageAmount;
    }

    public static decimal ModifyMusicNoteShotCount(ICombatState combatState, Creature? dealer, decimal amount,
        AbstractModel? source)
    {
        return combatState.IterateHookListeners().OfType<IMusicNoteModifyHookListener>().Aggregate(amount,
            (current, model) => model.ModifyMusicNoteShotCount(current, dealer, source));
    }

    public static decimal ModifyMusicNoteBounceCount(ICombatState combatState, Creature? dealer, decimal amount,
        AbstractModel? source)
    {
        return combatState.IterateHookListeners().OfType<IMusicNoteModifyHookListener>().Aggregate(amount,
            (current, model) => model.ModifyMusicNoteBounceCount(current, dealer, source));
    }

    public static async Task AfterCardSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Card.CombatState);

        foreach (var model in play.Card.CombatState.IterateHookListeners().OfType<ISubsideHookListener>())
        {
            await model.AfterCardSubside(choiceContext, play);
        }
    }

    public static async Task OnCardEnterPerformArea(
        PlayerChoiceContext choiceContext,
        ICombatState combatState,
        CardModel cardModel)
    {
        foreach (var model in combatState.IterateHookListeners().OfType<IPerformHookListener>())
        {
            await model.OnCardEnterPerformArea(choiceContext, cardModel);
        }
    }

    public static async Task OnCardLeavePerformArea(
        PlayerChoiceContext choiceContext,
        ICombatState combatState,
        CardModel cardModel)
    {
        foreach (var model in combatState.IterateHookListeners().OfType<IPerformHookListener>())
        {
            await model.OnCardLeavePerformArea(choiceContext, cardModel);
        }
    }

    public static async Task OnCardPerform(
        ICombatState combatState,
        PerformContext performContext,
        CardModel cardModel)
    {
        var netId = LocalContext.NetId;
        if (!netId.HasValue)
        {
            return;
        }

        foreach (var model in combatState.IterateHookListeners().OfType<IPerformHookListener>())
        {
            var choiceContext = new HookPlayerChoiceContext(cardModel, netId.Value, combatState, GameActionType.Combat);
            await model.OnCardPerform(choiceContext, performContext, cardModel);
        }
    }


    public static async Task OnMusicNoteSpawn(ICombatState combatState, VfxContext context, Player dealer)
    {
        foreach (var model in combatState.IterateHookListeners().OfType<IMusicNoteShotHookListener>())
            await model.OnMusicNoteSpawn(context, dealer);
    }

    public static async Task RunPerformHookAction(
        ICombatState combatState,
        AbstractModel source,
        Func<PlayerChoiceContext, Task> hook)
    {
        var netId = LocalContext.NetId;
        if (!netId.HasValue)
        {
            return;
        }

        var choiceContext = new HookPlayerChoiceContext(source, netId.Value, combatState, GameActionType.Combat);
        var task = hook(choiceContext);
        await choiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
        await choiceContext.WaitForCompletion();
    }
}