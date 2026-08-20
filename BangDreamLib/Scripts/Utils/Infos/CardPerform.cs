using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Utils.Infos;

/// <summary>
/// 表示一次独立的卡牌演奏。
/// </summary>
public sealed class CardPerform
{
    /// <summary>被演奏的卡牌。</summary>
    public required CardModel Card { get; init; }

    /// <summary>发起此次演奏的玩家。</summary>
    public required Player Player { get; init; }

    /// <summary>此次演奏发生时的插槽索引。</summary>
    public required int SlotIndex { get; init; }

    /// <summary>
    /// 此次演奏是否由系统或卡牌效果自动触发。
    /// False 表示由外部代码主动发起演奏。
    /// </summary>
    public required bool IsAutoPerform { get; init; }

    /// <summary>此次演奏是否为即兴演奏。</summary>
    public required bool IsInstant { get; init; }

    /// <summary>此次演奏是否由休止消耗余音触发。</summary>
    public required bool IsSubsideTriggered { get; init; }
}