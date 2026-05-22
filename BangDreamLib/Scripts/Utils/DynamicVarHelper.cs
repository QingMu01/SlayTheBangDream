using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace BangDreamLib.Scripts.Utils;

public static class DynamicVarHelper
{
    public static decimal ResolveBaseVar(CardModel? card, Creature? target, Func<CardModel, Creature?, decimal> calc)
    {
        ValidateStaticDelegate(calc);

        return card == null ? 0m : calc(card, target);
    }

    public static decimal ResolveBaseVar(CardModel? card, Func<CardModel?, decimal> calc)
    {
        ValidateStaticDelegate(calc);

        return card == null ? 0m : calc(card);
    }

    public static decimal ResolvePreviewDamageVar(CardModel? card, CardPreviewMode previewMode, Creature? target,
        bool runGlobalHooks, Func<CardModel?, decimal> calc)
    {
        ValidateStaticDelegate(calc);

        var baseVar = calc(card);

        if (card is { RunState: not null, CombatState: not null } && runGlobalHooks)
        {
            return Hook.ModifyDamage(
                card.RunState,
                card.CombatState,
                target,
                card.Owner.Creature,
                baseVar,
                ValueProp.Move,
                card,
                ModifyDamageHookType.All,
                previewMode,
                out _
            );
        }

        return baseVar;
    }

    public static decimal ResolvePreviewDamageVar(CardModel? card, CardPreviewMode previewMode, Creature? target,
        bool runGlobalHooks, Func<CardModel?, Creature?, decimal> calc)
    {
        ValidateStaticDelegate(calc);

        var baseVar = calc(card, target);

        if (card is { RunState: not null, CombatState: not null } && runGlobalHooks)
        {
            return Hook.ModifyDamage(
                card.RunState,
                card.CombatState,
                target,
                card.Owner.Creature,
                baseVar,
                ValueProp.Move,
                card,
                ModifyDamageHookType.All,
                previewMode,
                out _
            );
        }

        return baseVar;
    }

    public static decimal ResolvePreviewBlockVar(CardModel? card, CardPreviewMode previewMode, Creature? target,
        bool runGlobalHooks, Func<CardModel?, decimal> calc)
    {
        ValidateStaticDelegate(calc);

        var baseVar = calc(card);

        if (card is { CombatState: not null } && runGlobalHooks)
        {
            return Hook.ModifyBlock(
                card.CombatState,
                card.Owner.Creature,
                baseVar,
                ValueProp.Move,
                card,
                null,
                out _
            );
        }

        return baseVar;
    }

    private static void ValidateStaticDelegate(Delegate calc)
    {
        if (calc.Target != null)
        {
            throw new InvalidOperationException("Delegate must be static!");
        }
    }
}