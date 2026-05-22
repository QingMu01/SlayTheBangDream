using System.Reflection;
using BangDreamLib.Scripts.Attributes;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Content;

namespace BangDreamLib.Scripts.Utils;

public static class BangDreamRegisterHelper
{
    public static ModelType GetRegisterType(Type type)
    {
        if (type.IsSubclassOf(typeof(CardModel)))
        {
            return ModelType.Card;
        }

        if (type.IsSubclassOf(typeof(RelicModel)))
        {
            return ModelType.Relic;
        }

        if (type.IsSubclassOf(typeof(PotionModel)))
        {
            return ModelType.Potion;
        }

        if (type.IsSubclassOf(typeof(PowerModel)))
        {
            return ModelType.Power;
        }

        BangDreamLibCore.Logger.Warn($"{type.Name} is not registered.");
        return ModelType.Unknown;
    }
}

public enum ModelType
{
    Card,
    Relic,
    Potion,
    Power,
    Unknown
}

public static class ModelRegisterExtensions
{
    public static bool RegisterContent(this ModelType registry, Type type, ModContentRegistry contentRegistry)
    {
        var attr = type.GetCustomAttribute<BangDreamPoolAttribute>();
        if (attr != null)
        {
            switch (registry)
            {
                case ModelType.Card: contentRegistry.RegisterCard(attr.Pool, type); break;
                case ModelType.Relic: contentRegistry.RegisterRelic(attr.Pool, type); break;
                case ModelType.Potion: contentRegistry.RegisterPotion(attr.Pool, type); break;
                case ModelType.Power: contentRegistry.RegisterPower(type); break;
                case ModelType.Unknown:
                default: return false;
            }

            return true;
        }

        if (registry == ModelType.Power)
        {
            contentRegistry.RegisterPower(type);
            return true;
        }

        BangDreamLibCore.Logger.Warn($"Type = {type}(ModelType = {registry}) is unregistered.");
        return false;
    }
}