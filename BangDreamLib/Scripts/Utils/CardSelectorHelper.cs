using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Localization;

namespace BangDreamLib.Scripts.Utils;

public enum CardSelectorPrompt
{
    Empty,
    ToHand,
    ToPerformance,
    ToDraw,
    ToExtraDraw,
    ToDiscard,
    ToExhaust,
    ToPlay,
    ToRemove,
    ToUpgrade,
    ToEnchant,
    ToTransform
}

public static class SelectorPrefsExtensions
{
    private const string LocTable = "card_selection";

    private static readonly LocString Prompt = new(LocTable, "BANG_DREAM_LIB_PROMPT");
    private static readonly LocString RangePrompt = new(LocTable, "BANG_DREAM_LIB_PROMPT");
    private static readonly LocString LimitedPrompt = new(LocTable, "BANG_DREAM_LIB_PROMPT_LIMITED");
    private static readonly LocString UnlimitedPrompt = new(LocTable, "BANG_DREAM_LIB_PROMPT_UNLIMITED");

    private static readonly LocString Empty = new(LocTable, "BANG_DREAM_LIB_PROMPT_EMPTY");
    private static readonly LocString ToHand = new(LocTable, "BANG_DREAM_LIB_PROMPT_TO_HAND");
    private static readonly LocString ToPerformance = new(LocTable, "BANG_DREAM_LIB_PROMPT_TO_PERFORM");
    private static readonly LocString ToDraw = new(LocTable, "BANG_DREAM_LIB_PROMPT_TO_DRAW");
    private static readonly LocString ToExtraDraw = new(LocTable, "BANG_DREAM_LIB_PROMPT_TO_EXTRA_DRAW");
    private static readonly LocString ToDiscard = new(LocTable, "BANG_DREAM_LIB_PROMPT_TO_DISCARD");
    private static readonly LocString ToExhaust = new(LocTable, "BANG_DREAM_LIB_PROMPT_TO_EXHAUST");
    private static readonly LocString ToPlay = new(LocTable, "BANG_DREAM_LIB_PROMPT_TO_PLAY");
    private static readonly LocString ToRemove = new(LocTable, "BANG_DREAM_LIB_PROMPT_TO_REMOVE");
    private static readonly LocString ToUpgrade = new(LocTable, "BANG_DREAM_LIB_PROMPT_TO_UPGRADE");
    private static readonly LocString ToEnchant = new(LocTable, "BANG_DREAM_LIB_PROMPT_TO_ENCHANT");
    private static readonly LocString ToTransform = new(LocTable, "BANG_DREAM_LIB_PROMPT_TO_TRANSFORM");

    public static CardSelectorPrefs GetFixedPrefs(this CardSelectorPrompt prompt, int amount, bool cancelable = false,
        bool reqConfirm = false)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Invalid min or max value.");
        }

        var promptLocString = new LocString(Prompt.LocTable, Prompt.LocEntryKey);
        promptLocString.Add("To", GetDescription(prompt));
        return new CardSelectorPrefs(promptLocString, amount)
        {
            Cancelable = cancelable,
            RequireManualConfirmation = reqConfirm
        };
    }

    public static CardSelectorPrefs GetRangePrefs(this CardSelectorPrompt prompt, int min, int max,
        bool cancelable = false, bool reqConfirm = false)
    {
        if (min < 0 || max < 0 || min > max)
        {
            throw new ArgumentException("Invalid min or max value.");
        }

        var promptLocString = new LocString(RangePrompt.LocTable, RangePrompt.LocEntryKey);
        promptLocString.Add("To", GetDescription(prompt));
        return new CardSelectorPrefs(promptLocString, min, max)
        {
            Cancelable = cancelable,
            RequireManualConfirmation = reqConfirm
        };
    }

    public static CardSelectorPrefs GetLimitedPrefs(this CardSelectorPrompt prompt, int amount, bool cancelable = false,
        bool reqConfirm = false)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Invalid min or max value.");
        }

        var promptLocString = new LocString(LimitedPrompt.LocTable, LimitedPrompt.LocEntryKey);
        promptLocString.Add("To", GetDescription(prompt));
        return new CardSelectorPrefs(promptLocString, 0, amount)
        {
            Cancelable = cancelable,
            RequireManualConfirmation = reqConfirm
        };
    }

    public static CardSelectorPrefs GetUnlimitedPrefs(this CardSelectorPrompt prompt, bool cancelable = false,
        bool reqConfirm = false)
    {
        var promptLocString = new LocString(UnlimitedPrompt.LocTable, UnlimitedPrompt.LocEntryKey);
        promptLocString.Add("To", GetDescription(prompt));
        return new CardSelectorPrefs(promptLocString, 0, int.MaxValue)
        {
            Cancelable = cancelable,
            RequireManualConfirmation = reqConfirm
        };
    }

    private static LocString GetDescription(CardSelectorPrompt prompt)
    {
        return prompt switch
        {
            CardSelectorPrompt.Empty => Empty,
            CardSelectorPrompt.ToHand => ToHand,
            CardSelectorPrompt.ToPerformance => ToPerformance,
            CardSelectorPrompt.ToDraw => ToDraw,
            CardSelectorPrompt.ToExtraDraw => ToExtraDraw,
            CardSelectorPrompt.ToDiscard => ToDiscard,
            CardSelectorPrompt.ToExhaust => ToExhaust,
            CardSelectorPrompt.ToPlay => ToPlay,
            CardSelectorPrompt.ToRemove => ToRemove,
            CardSelectorPrompt.ToUpgrade => ToUpgrade,
            CardSelectorPrompt.ToEnchant => ToEnchant,
            CardSelectorPrompt.ToTransform => ToTransform,
            _ => Empty
        };
    }
}