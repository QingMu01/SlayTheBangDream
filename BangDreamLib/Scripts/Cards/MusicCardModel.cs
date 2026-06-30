using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Nodes.SubNode;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace BangDreamLib.Scripts.Cards;

public abstract class MusicCardModel(
    int baseCost,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true) : BandCardModel(baseCost, CardType.Quest, rarity, target, showInCardLibrary),
    IPerformanceCard
{
    public NPerformanceItem? Handle { get; set; }

    public virtual bool IsInstant { get; set; } = false;

    public virtual Task OnStartPerformance(PlayerChoiceContext choiceContext)
    {
        return Task.CompletedTask;
    }

    public virtual Task OnStopPerformance(PlayerChoiceContext choiceContext)
    {
        return Task.CompletedTask;
    }

    public virtual PileType StopPerformanceNextPile
    {
        get
        {
            var moveTo = base.GetResultPileTypeForCardPlay();
            return moveTo == PileType.Discard ? BangDreamConst.ExtraDraw : moveTo;
        }
    }

    protected sealed override PileType GetResultPileTypeForCardPlay()
    {
        return BangDreamConst.PerformanceTable;
    }
}