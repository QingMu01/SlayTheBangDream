using BangDreamLib.Scripts.Enums;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace BangDreamLib.Scripts.Interfaces.CardAugment;

public interface IPerformCard
{
    /// <summary>
    /// 即兴标记
    /// </summary>
    bool IsInstant { get; }

    /// <summary>
    /// 期望入队位置
    /// </summary>
    int AspirationSlot { get; }

    /// <summary>
    /// 入队策略
    /// </summary>
    PerformEnqueueStrategy Strategy { get; }

    /// <summary>
    /// 决定演奏中的牌被丢弃时候的牌堆
    /// </summary>
    (PileType, CardPilePosition) StopPerformanceNextPile();

    Task OnPerform(PlayerChoiceContext choiceContext);
}