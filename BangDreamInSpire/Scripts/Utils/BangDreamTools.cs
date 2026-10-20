using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Utils;

namespace BangDreamInSpire.Scripts.Utils;

public static class BangDreamTools
{
    public static T? LoadFromJson<T>(string filePath)
    {
        if (FileOperations.FileExists(filePath))
        {
            BangDreamInSpireCore.Logger.Info($"load json: {filePath}");
            return FileOperations.ReadJson<T>(filePath).Data;
        }

        BangDreamInSpireCore.Logger.Error($"file not found: {filePath}");
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
        return card is { IsMutable: true, Owner: not null, CombatState: not null, RunState: not null };
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

    /// <summary>
    /// 把乐队/角色主题色压到可以承载白字或作为细线的对比度：过亮的颜色（如 Crychic 的浅青）整体压暗。
    /// </summary>
    public static Color ToAccentColor(this Color color)
    {
        var luminance = 0.2126f * color.R + 0.7152f * color.G + 0.0722f * color.B;
        return luminance > 0.55f ? color.Darkened(0.45f) : color;
    }
}