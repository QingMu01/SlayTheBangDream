using BangDreamInSpire.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using STS2RitsuLib.Patching.Models;

namespace BangDreamInSpire.Scripts.Mechanics.Perform;

/// <summary>提供 Music 类型铭牌；Prefix 短路本体穷举 switch，避免新增类型抛异常。</summary>
internal class MusicCardTypePatch : IPatchMethod
{
    private static readonly LocString MusicType = new("gameplay_ui", "CARD_TYPE.MUSIC");

    public static string PatchId => "replace_music_card_type";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(CardTypeExtensions), nameof(CardTypeExtensions.ToLocString))];
    }

    public static bool Prefix(CardType cardType, ref LocString __result)
    {
        if (!BangDreamConst.IsMusicCardType(cardType))
        {
            return true;
        }

        __result = MusicType;
        return false;
    }
}
