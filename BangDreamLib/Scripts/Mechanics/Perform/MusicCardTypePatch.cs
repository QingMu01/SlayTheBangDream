using BangDreamLib.Scripts.Interfaces.CardAugment;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Cards;
using STS2RitsuLib.Patching.Models;

namespace BangDreamLib.Scripts.Mechanics.Perform;

/// <summary>
/// 将音乐牌（<see cref="IPerformCard" />）的卡面类型铭牌替换为"音乐"。
/// </summary>
internal class MusicCardTypePatch : IPatchMethod
{
    private static readonly LocString MusicType = new("gameplay_ui", "BANG_DREAM_LIB_MUSIC_TYPE");

    public static string PatchId => "replace_music_card_type";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCard), "UpdateTypePlaque")];
    }

    public static void Postfix(NCard __instance, MegaLabel ____typeLabel)
    {
        if (BangDreamCapabilities.IsPerformCard(__instance.Model))
        {
            ____typeLabel.SetTextAutoSize(MusicType.GetFormattedText());
        }
    }
}
