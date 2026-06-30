using System.Reflection;
using BangDreamLib.Scripts.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace BangDreamLib.Scripts.Patches;

public class CardKeywordPatches : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<CardKeywordDescriptionPatch>();
        patcher.RegisterPatch<CardKeywordHoverTipPatch>();
    }
}

internal class CardKeywordDescriptionPatch : IPatchMethod
{
    private static readonly LocString Instant = new("gameplay_ui", "BANG_DREAM_LIB_INSTANT_KEYWORD_TEXT");

    public static string PatchId => "added_description_where_card_has_keyword";

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(CardModel), "GetDescriptionForPile",
            [
                typeof(PileType),
                typeof(CardModel).GetNestedType("DescriptionPreviewType",BindingFlags.NonPublic | BindingFlags.Static)!,
                typeof(Creature)
            ])
        ];
    }

    public static void Postfix(CardModel __instance, ref string __result)
    {
        if (__instance.Keywords.Contains(BangDreamConst.Instant))
        {
            var cardText = Instant.GetFormattedText();
            if (!string.IsNullOrEmpty(cardText) && !string.IsNullOrEmpty(__result))
            {
                __result = $"[gold]{cardText}[/gold]\n{__result}";
            }
            else if (!string.IsNullOrEmpty(cardText))
            {
                __result = cardText;
            }
        }
    }
}

internal class CardKeywordHoverTipPatch : IPatchMethod
{
    public static string PatchId => "added_hovertip_where_card_has_keyword";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(CardModel), nameof(CardModel.HoverTips), MethodType.Getter)];
    }

    public static void Postfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        var hoverTips = __result.ToList();
        if (__instance.Keywords.Contains(BangDreamConst.Instant))
        {
            hoverTips.AddRange(BangDreamConst.Instant.GetModKeywordHoverTips());
            __result = hoverTips;
        }
    }
}