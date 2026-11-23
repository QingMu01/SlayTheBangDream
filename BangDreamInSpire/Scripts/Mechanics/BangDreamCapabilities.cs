using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Mechanics;

/// <summary>粗粒度能力判定，供装配、UI 和补丁使用。HasPerformArea 还要求方案槽位数大于 0，
/// 不同于裸接口判断。</summary>
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

    /// <summary>是否具有非空演奏方案。</summary>
    public static bool HasPerformArea(CharacterModel? character)
    {
        return character is IPerformableCharacter performable &&
               performable.CreatePerformScheme().TotalSlotCount > 0;
    }

    public static bool HasPerformArea(Player? player)
    {
        return HasPerformArea(player?.Character);
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
