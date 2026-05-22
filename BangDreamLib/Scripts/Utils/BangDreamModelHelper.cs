using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace BangDreamLib.Scripts.Utils;

public static class BangDreamModelHelper
{
    public static CardModel? GetCardModelByEntry(string entry)
    {
        var cardModel = ModelDb.AllCards.FirstOrDefault(cardModel => cardModel.Id.Entry == entry);
        if (cardModel != null)
        {
            return cardModel;
        }

        BangDreamLibCore.Logger.Error($"not found starting card: {entry}");
        return null;
    }

    public static RelicModel? GetRelicModelByEntry(string entry)
    {
        var relicModel = ModelDb.AllRelics.FirstOrDefault(relicModel => relicModel.Id.Entry == entry);
        if (relicModel != null)
        {
            return relicModel;
        }

        BangDreamLibCore.Logger.Error($"not found starting relic: {entry}");
        return null;
    }

    public static PotionModel? GetPotionModelByEntry(string entry)
    {
        var potionModel = ModelDb.AllPotions.FirstOrDefault(potionModel => potionModel.Id.Entry == entry);
        if (potionModel != null)
        {
            return potionModel;
        }

        BangDreamLibCore.Logger.Error($"not found starting potion: {entry}");
        return null;
    }

    public static CardCreationOptions CardCreationOptionsForRoom(CardPoolModel model, RoomType roomType)
    {
        var source = roomType switch
        {
            RoomType.Monster or RoomType.Elite or RoomType.Boss => CardCreationSource.Encounter,
            RoomType.Shop => CardCreationSource.Shop,
            RoomType.Event => throw new InvalidOperationException("ForRoom should not be used in event rooms"),
            _ => CardCreationSource.Other
        };

        var cardRarityOddsType = roomType switch
        {
            RoomType.Monster => CardRarityOddsType.RegularEncounter,
            RoomType.Elite => CardRarityOddsType.EliteEncounter,
            RoomType.Boss => CardRarityOddsType.BossEncounter,
            RoomType.Shop => CardRarityOddsType.Shop,
            _ => CardRarityOddsType.RegularEncounter
        };

        return new CardCreationOptions([model], source, cardRarityOddsType);
    }
}