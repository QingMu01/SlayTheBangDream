using System.Diagnostics.CodeAnalysis;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.SecondaryResources;

namespace BangDreamLib.Scripts.Extensions;

public static class DynamicVarsExtensions
{
    private const string BuffVarName = "Buff";

    private static readonly Dictionary<QuickVar, Func<int, DynamicVar>> QuickVarTypeMap = new()
    {
        [QuickVar.Cards] = baseValue => new CardsVar(baseValue),
        [QuickVar.Damage] = baseValue => new DamageVar(baseValue, ValueProp.Move),
        [QuickVar.Block] = baseValue => new BlockVar(baseValue, ValueProp.Move),
        [QuickVar.Energy] = baseValue => new EnergyVar(baseValue),
        [QuickVar.Gold] = baseValue => new GoldVar(baseValue),
        [QuickVar.Heal] = baseValue => new HealVar(baseValue),
        [QuickVar.HpLoss] = baseValue => new HpLossVar(baseValue),
        [QuickVar.Repeat] = baseValue => new RepeatVar(baseValue),
        [QuickVar.Buff] = baseValue => new IntVar(BuffVarName, baseValue),
        [QuickVar.Stars] = baseValue => new StarsVar(baseValue),
        [QuickVar.LingeredResource] = baseValue =>
            SecondaryResourceVars.For(nameof(BangDreamConst.LingeredResource), BangDreamConst.LingeredResource,
                baseValue)
    };

    private static readonly Dictionary<QuickVar, Func<string, int, DynamicVar>> QuickVarTypeWithNameMap = new()
    {
        [QuickVar.Cards] = (name, baseValue) => new CardsVar(name, baseValue),
        [QuickVar.Damage] = (name, baseValue) => new DamageVar(name, baseValue, ValueProp.Move),
        [QuickVar.Block] = (name, baseValue) => new BlockVar(name, baseValue, ValueProp.Move),
        [QuickVar.Energy] = (name, baseValue) => new EnergyVar(name, baseValue),
        [QuickVar.Gold] = (name, baseValue) => new GoldVar(name, baseValue),
        [QuickVar.Heal] = (name, baseValue) => new HealVar(name, baseValue),
        [QuickVar.HpLoss] = (name, baseValue) => new HpLossVar(name, baseValue),
        [QuickVar.Repeat] = (name, baseValue) => new RepeatVar(name, baseValue),
        [QuickVar.Buff] = (name, baseValue) => new IntVar(name, baseValue),
        [QuickVar.Stars] = (name, baseValue) => new StarsVar(name, baseValue),
        [QuickVar.LingeredResource] = (name, baseValue) =>
            SecondaryResourceVars.For(name, BangDreamConst.LingeredResource, baseValue)
    };

    private static readonly Dictionary<QuickVar, string> QuickVarDefaultNameMap = new()
    {
        [QuickVar.Cards] = CardsVar.defaultName,
        [QuickVar.Damage] = DamageVar.defaultName,
        [QuickVar.Block] = BlockVar.defaultName,
        [QuickVar.Energy] = EnergyVar.defaultName,
        [QuickVar.Gold] = GoldVar.defaultName,
        [QuickVar.Heal] = HealVar.defaultName,
        [QuickVar.HpLoss] = HpLossVar.defaultName,
        [QuickVar.Repeat] = RepeatVar.defaultName,
        [QuickVar.Buff] = BuffVarName,
        [QuickVar.Stars] = StarsVar.defaultName,
        [QuickVar.LingeredResource] = nameof(BangDreamConst.LingeredResource)
    };

    public static bool Var<T>(DynamicVarSet varSet, string name, [MaybeNullWhen(false)] out T var)
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

    public static decimal ComputedValue(this DynamicVarSet varSet, string name)
    {
        return varSet.ComputedValue(name, null);
    }

    public static decimal ComputedValue(this DynamicVarSet varSet, string name, Creature? target)
    {
        return Var<BangDreamComputedVar>(varSet, name, out var computedVar)
            ? computedVar.Calculate(target)
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

    public static DynamicVar GetVar(this QuickVar quickVar, CardModel cardModel, string? name = null)
    {
        var varName = name ?? QuickVarDefaultNameMap[quickVar];
        return cardModel.DynamicVars.TryGetValue(varName, out var dynamicVar)
            ? dynamicVar
            : throw new KeyNotFoundException($"Quick dynamic var {varName} not found");
    }
}

public enum QuickVar
{
    Cards,
    Damage,
    Block,
    Energy,
    Gold,
    Heal,
    HpLoss,
    Repeat,
    Buff,
    Stars,
    LingeredResource
}