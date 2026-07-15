using BangDreamLib.Scripts.Enums;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace BangDreamLib.Scripts.Cards;

public abstract class MusicCardModel(
    int baseCost,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true) : BandCardModel(baseCost, CardType.Quest, rarity, target, showInCardLibrary),
    IPerformCard
{
    public virtual bool IsInstant => false;
    public virtual int AspirationSlot => 0;
    public virtual PerformEnqueueStrategy Strategy => PerformEnqueueStrategy.Nearby;

    public virtual Task OnPerform(PlayerChoiceContext choiceContext)
    {
        return Task.CompletedTask;
    }

    public virtual (PileType, CardPilePosition) StopPerformanceNextPile()
    {
        var (moveTo, position) = base.GetResultPileTypeAndPositionForCardPlay();
        return (moveTo == PileType.Discard ? BangDreamConst.ExtraDraw : moveTo, position);
    }

    protected sealed override (PileType, CardPilePosition) GetResultPileTypeAndPositionForCardPlay()
    {
        return (BangDreamConst.PerformPile, CardPilePosition.Bottom);
    }
}