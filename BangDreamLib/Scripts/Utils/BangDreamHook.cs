using BangDreamLib.Scripts.Interfaces.GameHook;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace BangDreamLib.Scripts.Utils;

public static class BangDreamHook
{
    public static decimal ModifyLingeredEnergyAdd(ICombatState combatState, decimal amount)
    {
        foreach (var model in combatState.IterateHookListeners())
        {
            if (model is IModifyLingeredHook modifyHook)
            {
                amount = modifyHook.ModifyLingeredEnergyAdd(amount);
            }
        }

        return amount;
    }

    public static decimal ModifyLingeredEnergyReduce(ICombatState combatState, decimal amount)
    {
        foreach (var model in combatState.IterateHookListeners())
        {
            if (model is IModifyLingeredHook modifyHook)
            {
                amount = modifyHook.ModifyLingeredEnergyReduce(amount);
            }
        }

        return amount;
    }

    public static decimal ModifyMusicNoteDamage(IRunState runState,
        ICombatState? combatState,
        Creature? target,
        Creature? dealer,
        decimal damage,
        AbstractModel? source,
        ModifyDamageHookType modifyDamageHookType,
        out IEnumerable<AbstractModel> modifiers)
    {
        var modelModifiers = new List<AbstractModel>();
        var damageAmount = damage;
        if (modifyDamageHookType.HasFlag(ModifyDamageHookType.Additive))
        {
            foreach (var iterateHookListener in runState.IterateHookListeners(combatState))
            {
                if (iterateHookListener is IMusicNoteModifyHook modifyHook)
                {
                    var num = modifyHook.ModifyMusicNoteDamageAdditive(target, damageAmount, dealer, source);
                    damageAmount += num;
                    if (num != 0M)
                        modelModifiers.Add(iterateHookListener);
                }
            }
        }

        if (modifyDamageHookType.HasFlag(ModifyDamageHookType.Multiplicative))
        {
            foreach (var iterateHookListener in runState.IterateHookListeners(combatState))
            {
                if (iterateHookListener is IMusicNoteModifyHook modifyHook)
                {
                    var num = modifyHook.ModifyMusicNoteDamageMultiplicative(target, damageAmount, dealer,
                        source);
                    damageAmount *= num;
                    if (num != 1M)
                        modelModifiers.Add(iterateHookListener);
                }
            }
        }

        modifiers = modelModifiers;
        return damageAmount;
    }

    public static decimal ModifyMusicNoteShotCount(ICombatState combatState, Creature? dealer, decimal amount,
        AbstractModel? source)
    {
        foreach (var model in combatState.IterateHookListeners())
        {
            if (model is IMusicNoteModifyHook modifyHook)
            {
                amount = modifyHook.ModifyMusicNoteShotCount(amount, dealer, source);
            }
        }

        return amount;
    }

    public static async Task AfterLingeredEnergyAdded(ICombatState combatState, Player player, int amount)
    {
        foreach (var model in combatState.IterateHookListeners())
        {
            if (model is ILingeredChangedHook changedHook)
            {
                await changedHook.AfterLingeredEnergyAdded(player, amount);
            }
        }
    }

    public static async Task AfterLingeredEnergyReduced(ICombatState combatState, Player player, int amount)
    {
        foreach (var model in combatState.IterateHookListeners())
        {
            if (model is ILingeredChangedHook changedHook)
            {
                await changedHook.AfterLingeredEnergyReduced(player, amount);
            }
        }
    }

    public static async Task OnLingeredEnergyFilled(ICombatState combatState, Player player, int amount)
    {
        foreach (var model in combatState.IterateHookListeners())
        {
            if (model is ILingeredChangedHook changedHook)
            {
                await changedHook.OnLingeredEnergyFilled(player, amount);
            }
        }
    }

    public static async Task FinalUnprocessedOverflow(ICombatState combatState, Player player, int amount)
    {
        foreach (var model in combatState.IterateHookListeners())
        {
            if (model is ILingeredChangedHook changedHook)
            {
                await changedHook.FinalUnprocessedOverflow(player, amount);
            }
        }
    }


    public static async Task OnCardEnterPerformanceArea(ICombatState combatState, CardModel cardModel)
    {
        foreach (var model in combatState.IterateHookListeners())
        {
            if (model != cardModel && model is ICardPerformanceHook performanceHook)
            {
                await performanceHook.OnCardEnterPerformanceArea(cardModel);
            }
        }
    }

    public static async Task OnCardLeavePerformanceArea(ICombatState combatState, CardModel cardModel)
    {
        foreach (var model in combatState.IterateHookListeners())
        {
            if (model != cardModel && model is ICardPerformanceHook performanceHook)
            {
                await performanceHook.OnCardLeavePerformanceArea(cardModel);
            }
        }
    }

    public static async Task OnMusicNotePlayed(ICombatState combatState, Player source)
    {
        foreach (var model in combatState.IterateHookListeners())
        {
            if (model is IMusicNotePlayedHook playedHook)
            {
                await playedHook.OnMusicNotePlayed(new HookPlayerChoiceContext(source, source.NetId,
                    GameActionType.Combat));
            }
        }
    }
}