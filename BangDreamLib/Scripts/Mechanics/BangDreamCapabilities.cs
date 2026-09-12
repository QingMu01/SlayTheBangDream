using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Mechanics;

/// <summary>
/// 角色与卡牌能力的统一判定入口。机制实现与补丁一律通过这里判断能力，
/// 避免在库代码中散落 <c>is IXxxCharacter</c> / <c>is IXxxCard</c> 判定，并使新角色只需实现接口即可自动接入。
/// </summary>
public static class BangDreamCapabilities
{
    public static bool HasExtraDeck(CharacterModel? character)
    {
        return character is IExtraDeckSupportCharacter;
    }

    public static bool HasExtraDeck(Player? player)
    {
        return HasExtraDeck(player?.Character);
    }

    public static bool HasPerform(CharacterModel? character)
    {
        return character is IPerformableCharacter { GetDefaultCapacity: > 0 };
    }

    public static bool HasPerform(Player? player)
    {
        return HasPerform(player?.Character);
    }

    public static bool HasLingered(CharacterModel? character)
    {
        return character is ILingeredResourceCharacter;
    }

    public static bool HasLingered(Player? player)
    {
        return HasLingered(player?.Character);
    }

    public static bool IsPerformCard(CardModel? card)
    {
        return card is IPerformCard;
    }

    public static bool IsSubsideCard(CardModel? card)
    {
        return card is ISubsideCard;
    }
}
