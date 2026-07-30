using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Utils;

namespace BangDreamLib.Scripts.Utils;

public static class BangDreamTools
{
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

    public static IEnumerable<CardModel> GetCharacterExtraCards(Player player, bool onlyMusic = false,
        bool fallbackToAllChar = true)
    {
        if (onlyMusic)
        {
            if (player.Character is IPerformableCharacter performableCharacter)
            {
                return performableCharacter.ExtraCardPool.AllCards.Where(card => card is IPerformCard);
            }
        }
        else
        {
            if (player.Character is IExtraDeckSupportCharacter extraDeckSupportCharacter)
            {
                return extraDeckSupportCharacter.ExtraCardPool.AllCards;
            }
        }

        if (fallbackToAllChar)
        {
            return ModelDb.AllCharacters.OfType<IExtraDeckSupportCharacter>()
                .SelectMany(character => character.ExtraCardPool.AllCards)
                .Where(card => !onlyMusic || card is IPerformCard);
        }

        return [];
    }

    public static bool CardIsInCombat(CardModel? card)
    {
        return card is { IsMutable: true, Owner: not null };
    }

    public static void Init<T>(ref T? storage, T value, string propertyName) where T : class
    {
        if (storage is not null)
            throw new InvalidOperationException($"{propertyName} is already initialized!");
        storage = value;
    }

    public static void Init<T>(ref T? storage, T value, string propertyName) where T : struct
    {
        if (storage.HasValue)
            throw new InvalidOperationException($"{propertyName} is already initialized!");
        storage = value;
    }
}