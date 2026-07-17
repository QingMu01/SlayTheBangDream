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

    public virtual CardLocation StopPerformanceNextPile()
    {
        var location = base.GetResultLocationForCardPlay();
        return location.pileType == PileType.Discard
            ? new CardLocation(Owner, BangDreamConst.ExtraDraw, CardPilePosition.Bottom)
            : location;
    }

    protected sealed override CardLocation GetResultLocationForCardPlay()
    {
        return new CardLocation(Owner, BangDreamConst.PerformPile, CardPilePosition.Bottom);
    }
}