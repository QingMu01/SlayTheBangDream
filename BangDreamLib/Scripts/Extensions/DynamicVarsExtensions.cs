using System.Diagnostics.CodeAnalysis;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;

namespace BangDreamLib.Scripts.Extensions;

public static class DynamicVarsExtensions
{
    private static readonly Dictionary<QuickVar, Func<int, DynamicVar>> QuickVarTypeMap = new()
    {
        [QuickVar.Cards] = baseValue => new CardsVar(baseValue),
        [QuickVar.Damage] = baseValue => new DamageVar(baseValue, ValueProp.Move),
        [QuickVar.Block] = baseValue => new BlockVar(baseValue, ValueProp.Move),
        [QuickVar.Energy] = baseValue => new EnergyVar(baseValue),
        [QuickVar.LingeredEnergy] = baseValue => new IntVar("LingeredEnergy", baseValue),
        [QuickVar.Gold] = baseValue => new GoldVar(baseValue),
        [QuickVar.Heal] = baseValue => new HealVar(baseValue),
        [QuickVar.HpLoss] = baseValue => new HpLossVar(baseValue),
        [QuickVar.Repeat] = baseValue => new RepeatVar(baseValue),
        [QuickVar.Stars] = baseValue => new StarsVar(baseValue)
    };

    private static readonly Dictionary<QuickVar, Func<string, int, DynamicVar>> QuickVarTypeWithNameMap = new()
    {
        [QuickVar.Cards] = (name, baseValue) => new CardsVar(name, baseValue),
        [QuickVar.Damage] = (name, baseValue) => new DamageVar(name, baseValue, ValueProp.Move),
        [QuickVar.Block] = (name, baseValue) => new BlockVar(name, baseValue, ValueProp.Move),
        [QuickVar.Energy] = (name, baseValue) => new EnergyVar(name, baseValue),
        [QuickVar.LingeredEnergy] = (name, baseValue) => new IntVar(name, baseValue),
        [QuickVar.Gold] = (name, baseValue) => new GoldVar(name, baseValue),
        [QuickVar.Heal] = (name, baseValue) => new HealVar(name, baseValue),
        [QuickVar.HpLoss] = (name, baseValue) => new HpLossVar(name, baseValue),
        [QuickVar.Repeat] = (name, baseValue) => new RepeatVar(name, baseValue),
        [QuickVar.Stars] = (name, baseValue) => new StarsVar(name, baseValue)
    };

    private static readonly Dictionary<QuickVar, string> QuickVarNameMap = new()
    {
        [QuickVar.Cards] = nameof(DynamicVarSet.Cards),
        [QuickVar.Damage] = nameof(DynamicVarSet.Damage),
        [QuickVar.Block] = nameof(DynamicVarSet.Block),
        [QuickVar.Energy] = nameof(DynamicVarSet.Energy),
        [QuickVar.LingeredEnergy] = "LingeredEnergy",
        [QuickVar.Gold] = nameof(DynamicVarSet.Gold),
        [QuickVar.Heal] = nameof(DynamicVarSet.Heal),
        [QuickVar.HpLoss] = nameof(DynamicVarSet.HpLoss),
        [QuickVar.Repeat] = nameof(DynamicVarSet.Repeat),
        [QuickVar.Stars] = nameof(DynamicVarSet.Stars)
    };

    private static bool Var<T>(DynamicVarSet varSet, string name, [MaybeNullWhen(false)] out T var)
        where T : DynamicVar
    {
        var = null;
        if (varSet.TryGetValue(name, out var dynamicVar))
        {
            if (dynamicVar is T targetVar)
            {
                var = targetVar;
                return true;
            }

            BangDreamLibCore.Logger.Warn($"Find dynamic var ({name}) but type  is not {typeof(T)}");
        }

        return false;
    }

    public static ComputedDynamicVar ComputeVar(this DynamicVarSet varSet, string name)
    {
        return Var<ComputedDynamicVar>(varSet, name, out var computedVar)
            ? computedVar
            : throw new KeyNotFoundException($"Compute dynamic var {name} not found");
    }

    public static DynamicVar Create(this QuickVar quickVar, string name, int baseValue)
    {
        return QuickVarTypeWithNameMap[quickVar].Invoke(name, baseValue);
    }

    public static DynamicVar Create(this QuickVar quickVar, int baseValue)
    {
        return QuickVarTypeMap[quickVar].Invoke(baseValue);
    }

    public static DynamicVar Get(this QuickVar quickVar, DynamicVarSet varSet, string name)
    {
        return Var<DynamicVar>(varSet, name, out var dynamicVar)
            ? dynamicVar
            : throw new KeyNotFoundException($"QuickVar {quickVar} not found");
    }

    public static DynamicVar Get(this QuickVar quickVar, DynamicVarSet varSet)
    {
        return quickVar.Get(varSet, QuickVarNameMap[quickVar]);
    }
}

public enum QuickVar
{
    Cards,
    Damage,
    Block,
    Energy,
    LingeredEnergy,
    Gold,
    Heal,
    HpLoss,
    Repeat,
    Stars
}