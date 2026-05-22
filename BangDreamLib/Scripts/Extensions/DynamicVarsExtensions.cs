using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace BangDreamLib.Scripts.Extensions;

public static class DynamicVarsExtensions
{
    private static readonly Dictionary<QuickVar, Func<int, DynamicVar>> QuickVarTypeMap = new()
    {
        [QuickVar.Cards] = baseValue => new CardsVar(baseValue),
        [QuickVar.Damage] = baseValue => new DamageVar(baseValue, ValueProp.Move),
        [QuickVar.Block] = baseValue => new BlockVar(baseValue, ValueProp.Move),
        [QuickVar.Energy] = baseValue => new EnergyVar(baseValue),
        [QuickVar.Subside] = baseValue => new IntVar("Subside", baseValue),
        [QuickVar.Gold] = baseValue => new GoldVar(baseValue),
        [QuickVar.Heal] = baseValue => new HealVar(baseValue),
        [QuickVar.HpLoss] = baseValue => new HpLossVar(baseValue),
        [QuickVar.Repeat] = baseValue => new RepeatVar(baseValue),
        [QuickVar.Stars] = baseValue => new StarsVar(baseValue)
    };

    private static readonly Dictionary<QuickVar, string> QuickVarNameMap = new()
    {
        [QuickVar.Cards] = nameof(DynamicVarSet.Cards),
        [QuickVar.Damage] = nameof(DynamicVarSet.Damage),
        [QuickVar.Block] = nameof(DynamicVarSet.Block),
        [QuickVar.Energy] = nameof(DynamicVarSet.Energy),
        [QuickVar.Subside] = "Subside",
        [QuickVar.Gold] = nameof(DynamicVarSet.Gold),
        [QuickVar.Heal] = nameof(DynamicVarSet.Heal),
        [QuickVar.HpLoss] = nameof(DynamicVarSet.HpLoss),
        [QuickVar.Repeat] = nameof(DynamicVarSet.Repeat),
        [QuickVar.Stars] = nameof(DynamicVarSet.Stars)
    };

    public static T DynamicVar<T>(this CardModel cardModel, string name) where T : DynamicVar
    {
        return cardModel.DynamicVars.VarOrNull<T>(name) ??
               throw new ArgumentNullException($"Card ({cardModel}) does not have dynamic var: {name}");
    }

    public static DynamicVar DynamicVar(this CardModel cardModel, string name)
    {
        return cardModel.DynamicVar<DynamicVar>(name);
    }

    public static T DynamicVar<T>(this PowerModel powerModel, string name) where T : DynamicVar
    {
        return powerModel.DynamicVars.VarOrNull<T>(name) ??
               throw new ArgumentNullException($"Power ({powerModel}) does not have dynamic var: {name}");
    }

    public static DynamicVar DynamicVar(this PowerModel powerModel, string name)
    {
        return powerModel.DynamicVar<DynamicVar>(name);
    }

    public static T DynamicVar<T>(this RelicModel relicModel, string name) where T : DynamicVar
    {
        return relicModel.DynamicVars.VarOrNull<T>(name) ??
               throw new ArgumentNullException($"Relic ({relicModel}) does not have dynamic var: {name}");
    }

    public static DynamicVar DynamicVar(this RelicModel relicModel, string name)
    {
        return relicModel.DynamicVar<DynamicVar>(name);
    }

    public static T Var<T>(this DynamicVarSet varSet, string name) where T : DynamicVar
    {
        if (varSet.TryGetValue(name, out var dynamicVar))
        {
            if (dynamicVar is T targetVar)
            {
                return targetVar;
            }

            BangDreamLibCore.Logger.Warn($"Dynamic var ({name}) is not {typeof(T)}");
        }

        throw new ArgumentNullException($"Dynamic var ({name}) does not exist.");
    }

    public static T? VarOrNull<T>(this DynamicVarSet varSet, string name) where T : DynamicVar
    {
        if (varSet.TryGetValue(name, out var dynamicVar))
        {
            if (dynamicVar is T targetVar)
            {
                return targetVar;
            }

            BangDreamLibCore.Logger.Warn($"Dynamic var ({name}) is not {typeof(T)}");
        }

        return null;
    }

    public static DynamicVar Create(this QuickVar quickVar, int baseValue)
    {
        return QuickVarTypeMap[quickVar].Invoke(baseValue);
    }

    public static DynamicVar Get(this QuickVar quickVar, DynamicVarSet varSet)
    {
        return varSet.Var<DynamicVar>(QuickVarNameMap[quickVar]);
    }
}

public enum QuickVar
{
    Cards,
    Damage,
    Block,
    Energy,
    Subside,
    Gold,
    Heal,
    HpLoss,
    Repeat,
    Stars
}