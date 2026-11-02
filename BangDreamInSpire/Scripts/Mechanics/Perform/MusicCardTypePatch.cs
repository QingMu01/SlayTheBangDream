using BangDreamInSpire.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using STS2RitsuLib.Patching.Models;

namespace BangDreamInSpire.Scripts.Mechanics.Perform;

/// <summary>
///     为新增卡牌类型 <c>Music</c> 提供类型铭牌本地化。
///     <para>
///         本体 <see cref="CardTypeExtensions.ToLocString" /> 是穷举 switch 表达式且没有 default，
///         新枚举值会在 <c>NCard.UpdateTypePlaque</c> 处抛 <see cref="System.Runtime.CompilerServices.SwitchExpressionException" />。
///         必须用 Prefix 直接短路：Postfix 在原方法抛异常时根本不会执行。
///     </para>
/// </summary>
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
