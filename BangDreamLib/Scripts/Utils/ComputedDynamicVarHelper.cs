using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.ValueProps;

namespace BangDreamLib.Scripts.Utils;

public static class ComputedDynamicVarHelper
{
    public static BangDreamComputedVar CreateBaseVar(string name, decimal baseValue,
        Func<BangDreamComputedVar.ComputedVarsContext, decimal> calc)
    {
        return new BangDreamComputedVar(name, baseValue, calc);
    }

    public static BangDreamComputedVar CreateDamageVar(string name, decimal baseValue,
        Func<BangDreamComputedVar.ComputedVarsContext, decimal> calc, ValueProp prop = ValueProp.Move)
    {
        return new BangDreamComputedVar(name, baseValue, calc,
            (ctx, mode, runHooks) => ApplyHooks(ctx, mode, runHooks, prop, true, calc)
        );
    }

    public static BangDreamComputedVar CreateBlockVar(string name, decimal baseValue,
        Func<BangDreamComputedVar.ComputedVarsContext, decimal> calc, ValueProp prop = ValueProp.Move)
    {
        return new BangDreamComputedVar(name, baseValue, calc,
            (ctx, mode, runHooks) => ApplyHooks(ctx, mode, runHooks, prop, false, calc)
        );
    }

    private static decimal ApplyHooks(BangDreamComputedVar.ComputedVarsContext ctx, CardPreviewMode mode,
        bool runHooks, ValueProp prop, bool isDamage,
        Func<BangDreamComputedVar.ComputedVarsContext, decimal> calc)
    {
        if (ctx.IsInCombat())
        {
            var calcValue = calc(ctx);
            if (runHooks)
            {
                return isDamage
                    ? Hook.ModifyDamage(ctx.ActiveRunState, ctx.ActiveCombatState, ctx.Target,
                        ctx.ActiveCard.Owner.Creature, calcValue, prop, ctx.ActiveCard, null,
                        ModifyDamageHookType.All, mode, out _)
                    : Hook.ModifyBlock(ctx.ActiveCombatState, ctx.ActiveCard.Owner.Creature, calcValue, prop,
                        ctx.ActiveCard, null, out _);
            }

            return calcValue;
        }

        return ctx.BaseValue;
    }
}