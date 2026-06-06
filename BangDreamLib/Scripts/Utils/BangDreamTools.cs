using System.Reflection;
using BangDreamLib.Scripts.Attributes;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Utils;

namespace BangDreamLib.Scripts.Utils;

public static class BangDreamTools
{
    public static RunState? RunState { get; set; }

    public static Player? LocalPlayer { get; set; }

    public static CardPile GetPile(PileType type, Player player)
    {
        return CardPile.Get(type, player) ?? throw new NullReferenceException("card pile is not ready.");
    }

    public static T? LoadFromJson<T>(string filePath)
    {
        if (FileOperations.FileExists(filePath))
        {
            BangDreamLibCore.Logger.Info($"load json: {filePath}");
            return FileOperations.ReadJson<T>(filePath).Data;
        }

        BangDreamLibCore.Logger.Error($"file not found: {filePath}");
        return default;
    }

    public static List<Type> CollectAllModels(Assembly assembly)
    {
        return assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<BangDreamIgnoreAttribute>() == null)
            .Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(AbstractModel)))
            .Where(type => !type.IsSubclassOf(typeof(CardPoolModel)))
            .Where(type => !type.IsSubclassOf(typeof(RelicPoolModel)))
            .Where(type => !type.IsSubclassOf(typeof(PotionPoolModel)))
            .Where(type => !type.IsSubclassOf(typeof(CharacterModel)))
            .Where(type => !type.IsSubclassOf(typeof(MonsterModel)))
            .Where(type => !type.IsSubclassOf(typeof(EventModel)))
            .ToList();
    }
}