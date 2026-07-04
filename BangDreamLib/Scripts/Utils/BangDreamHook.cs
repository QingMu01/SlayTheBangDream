using BangDreamLib.Scripts.Interfaces.GameHook;
using MegaCrit.Sts2.Core.Combat;
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
            damageAmount = combatState.IterateHookListeners().OfType<IMusicNoteModifyHook>()
                .Aggregate(damageAmount,
                    (current, modifyHook) =>
                        current + modifyHook.ModifyMusicNoteDamageAdditive(target, current, dealer, source));
        }

        if (modifyDamageHookType.HasFlag(ModifyDamageHookType.Multiplicative))
        {
            damageAmount = combatState.IterateHookListeners().OfType<IMusicNoteModifyHook>()
                .Aggregate(damageAmount,
                    (current, modifyHook) =>
                        current * modifyHook.ModifyMusicNoteDamageMultiplicative(target, current, dealer, source));
        }

        return damageAmount;
    }

    public static decimal ModifyMusicNoteShotCount(ICombatState combatState, Creature? dealer, decimal amount,
        AbstractModel? source)
    {
        return combatState.IterateHookListeners().OfType<IMusicNoteModifyHook>().Aggregate(amount,
            (current, model) => model.ModifyMusicNoteShotCount(current, dealer, source));
    }

    public static async Task OnCardEnterPerformanceArea(ICombatState combatState, CardModel cardModel)
    {
        foreach (var model in combatState.IterateHookListeners().OfType<ICardPerformanceHook>())
        {
            if (model != cardModel)
            {
                await model.OnCardEnterPerformanceArea(cardModel);
            }
        }
    }

    public static async Task OnCardLeavePerformanceArea(ICombatState combatState, CardModel cardModel)
    {
        foreach (var model in combatState.IterateHookListeners().OfType<ICardPerformanceHook>())
        {
            if (model != cardModel)
            {
                await model.OnCardLeavePerformanceArea(cardModel);
            }
        }
    }

    public static async Task OnMusicNotePlayed(ICombatState combatState, Player source)
    {
        foreach (var model in combatState.IterateHookListeners().OfType<IMusicNotePlayedHook>())
        {
            await model.OnMusicNotePlayed(new HookPlayerChoiceContext(source, source.NetId, GameActionType.Combat));
        }
    }
}