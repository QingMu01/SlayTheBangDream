using BangDreamLib.Scripts.Nodes.SubNode;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace BangDreamLib.Scripts.Interfaces.CardAugment;

public interface IPerformanceCard
{
    bool IsInstant { get; set; }
    PileType WhenStopMoveToPile { get; }
    NPerformanceItem? Handle { get; set; }

    Task OnStartPerformance(PlayerChoiceContext choiceContext);

    Task OnStopPerformance(PlayerChoiceContext choiceContext);
}