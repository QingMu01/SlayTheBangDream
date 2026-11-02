using BangDreamInSpire.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using STS2RitsuLib.Patching.Models;

namespace BangDreamInSpire.Scripts.Mechanics.Perform;

/// <summary>
///     为新增卡牌类型 <c>Music</c> 提供类型铭牌本地化。
///     <para>
///         本体 <see cref="CardTypeExtensions.ToLocString" /> 是穷举 switch 表达式且没有 default，
///         新枚举值会在 <c>NCard.UpdateTypePlaque</c> 处抛 <see cref="System.Runtime.CompilerServices.SwitchExpressionException" />；
///         这里补上 Music 的 LocString 映射（原实现是覆盖 NCard 铭牌文本，属于同一语义的旧做法）。
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

    public static void Postfix(CardType cardType, ref LocString __result)
    {
        if (BangDreamConst.IsMusicCardType(cardType))
        {
            __result = MusicType;
        }
    }
}
