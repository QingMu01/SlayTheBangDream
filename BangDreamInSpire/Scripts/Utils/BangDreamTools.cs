using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Utils;

namespace BangDreamInSpire.Scripts.Utils;

public static class BangDreamTools
{
    /// <summary>
    /// 配置 MegaLabel / MegaRichTextLabel 的自动缩放区间。
    /// 注意：不要把这些导出属性写进 .tscn —— 在编辑器里保存场景时 C# 导出属性会被写成 null，
    /// 因此统一在父节点的 _EnterTree 中、子节点 _Ready 之前用代码配置。
    /// </summary>
    public static void ConfigureMegaText(Control? control, int maxFontSize, int minFontSize, bool autoSize = true)
    {
        switch (control)
        {
            case MegaLabel label:
                label.AutoSizeEnabled = autoSize;
                label.MaxFontSize = maxFontSize;
                label.MinFontSize = minFontSize;
                break;
            case MegaRichTextLabel richText:
                richText.AutoSizeEnabled = autoSize;
                richText.MaxFontSize = maxFontSize;
                richText.MinFontSize = minFontSize;
                break;
        }
    }

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
        var luminance = GetLuminance(color);
        return luminance > 0.55f ? color.Darkened(0.45f) : color;
    }

    /// <summary>
    /// 返回能压在该背景色上阅读的文字色：亮底配深字，暗底配白字。
    /// </summary>
    public static Color GetReadableTextColor(this Color background)
    {
        return GetLuminance(background) > 0.55f
            ? new Color(0.086f, 0.125f, 0.18f)
            : Colors.White;
    }

    private static float GetLuminance(Color color)
    {
        return 0.2126f * color.R + 0.7152f * color.G + 0.0722f * color.B;
    }
}