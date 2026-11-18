using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Mechanics;

/// <summary>
/// 角色与卡牌能力的**粗粒度**判定入口，只服务装配 / UI / 补丁这类需要「是否具备某能力」的场合：
/// 决定是否装配对应机制、是否显示对应 UI、或补丁是否需要进入某条分支。
/// </summary>
/// <remarks>
/// <para>
/// 边界：本门面**不是**全仓唯一的判定口径，也不要求把已有的裸判逐一收口。核心内部需要访问接口成员
/// （读方案、拿槽位、取角色专属配置等）时，仍可直接写 <c>is IXxx</c>——
/// 例如 <c>ExtraDeckMechanic</c> 里读 <c>IExtraDeckSupportCharacter.ShouldAlwaysShowExtraDeck</c> 的裸判。
/// 裸判与门面的取舍看调用点要的是「实现了接口」还是「具备可用的能力」。
/// </para>
/// <para>
/// 语义差异尤其注意：<c>HasPerformArea</c> = 实现演奏接口**且**方案槽位总数 &gt; 0（可用于创建 / 显示演奏区）；
/// 裸 <c>is IPerformableCharacter</c> 只表示「实现了接口」。两者不可混用：需要 0 槽位角色也能命中时用裸判，
/// 判断「是否有可用的演奏区」时才用 <c>HasPerformArea</c>。
/// </para>
/// </remarks>
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

    /// <summary>是否具备可用的演奏区：实现演奏接口且方案槽位总数 &gt; 0（裸 <c>is IPerformableCharacter</c> 不含后半个条件）。</summary>
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
