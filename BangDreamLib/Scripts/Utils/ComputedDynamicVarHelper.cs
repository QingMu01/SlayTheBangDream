using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;

namespace BangDreamLib.Scripts.Utils;

public static class ComputedDynamicVarHelper
{
    public static ComputedDynamicVar CreateBaseVar(string name, decimal baseValue,
        Func<CardModel?, Creature?, decimal> calc)
    {
        return ModCardVars.Computed(name, baseValue,
            calc,
            (card, _, target, _) => calc(card, target));
    }

    public static ComputedDynamicVar CreateBaseVar(string name, decimal baseValue,
        Func<CardModel?, decimal> calc)
    {
        return ModCardVars.Computed(name, baseValue,
            (card, _) => calc(card),
            (card, _, _, _) => calc(card));
    }

    public static ComputedDynamicVar CreateDamageVar(string name, decimal baseValue,
        Func<CardModel?, Creature?, decimal> calc, ValueProp prop = ValueProp.Move)
    {
        return ModCardVars.Computed(name, baseValue,
            calc,
            (card, mode, target, runHooks) => ApplyHooks(card, mode, target, runHooks, prop, true, calc)
        );
    }

    public static ComputedDynamicVar CreateBlockVar(string name, decimal baseValue,
        Func<CardModel?, Creature?, decimal> calc, ValueProp prop = ValueProp.Move)
    {
        return ModCardVars.Computed(name, baseValue,
            calc,
            (card, mode, target, runHooks) => ApplyHooks(card, mode, target, runHooks, prop, false, calc)
        );
    }

    private static decimal ApplyHooks(CardModel? card, CardPreviewMode mode, Creature? target,
        bool runHooks,
        ValueProp prop, bool isDamage, Func<CardModel?, Creature?, decimal> calc)
    {
        var baseValue = calc(card, target);

        if (card is { RunState: not null, CombatState: not null } && runHooks)
        {
            return isDamage
                ? Hook.ModifyDamage(card.RunState, card.CombatState, target, card.Owner.Creature,
                    baseValue, prop, card, null, ModifyDamageHookType.All, mode, out _)
                : Hook.ModifyBlock(card.CombatState, card.Owner.Creature, baseValue, prop, card, null, out _);
        }

        return baseValue;
    }
}