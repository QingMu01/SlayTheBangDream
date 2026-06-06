using BangDreamLib.Scripts.Nodes.SubNode;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Utils;

namespace BangDreamLib.Scripts.Interfaces.CardAugment;

public interface IPerformanceCard
{
    public static readonly AttachedState<CardModel, bool> CardEnterExtraDeck = new(_ => false);

    bool IsInstant { get; set; }
    PileType StopPerformanceNextPile { get; }
    NPerformanceItem? Handle { get; set; }

    Task OnStartPerformance(PlayerChoiceContext choiceContext);

    Task OnStopPerformance(PlayerChoiceContext choiceContext);
}