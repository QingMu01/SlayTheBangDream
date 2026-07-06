using BangDreamLib.Scripts.Nodes.SubNode;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace BangDreamLib.Scripts.Interfaces.CardAugment;

public interface IPerformCard
{
    /// <summary>
    /// 即兴标记
    /// 即兴牌能无视容量地进入演奏区，但在生效后会被立刻弃置
    /// </summary>
    bool IsInstant { get; set; }

    /// <summary>
    /// 决定演奏中的牌被丢弃时候的牌堆
    /// </summary>
    (PileType, CardPilePosition) StopPerformanceNextPile { get; }

    /// <summary>
    /// 演奏句柄
    /// </summary>
    NPerformItem? Handle { get; set; }

    Task OnStartPerform(PlayerChoiceContext choiceContext);

    Task OnStopPerform(PlayerChoiceContext choiceContext);
}