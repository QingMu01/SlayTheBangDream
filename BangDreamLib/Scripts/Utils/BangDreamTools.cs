using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using STS2RitsuLib.Utils;

namespace BangDreamLib.Scripts.Utils;

public static class BangDreamTools
{
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
}