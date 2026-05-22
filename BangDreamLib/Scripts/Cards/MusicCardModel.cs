using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Nodes.SubNode;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Scaffolding.Content;

namespace BangDreamLib.Scripts.Cards;

public abstract class MusicCardModel(
    int baseCost,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true) : ModCardTemplate(baseCost, CardType.Quest, rarity, target, showInCardLibrary),
    IPerformanceCard
{
    public NPerformanceItem? Handle { get; set; }

    protected abstract CardAssetProfile CardAssetProfile { get; }

    protected virtual IEnumerable<CardKeyword> CardKeywords => [];
    protected virtual IEnumerable<IHoverTip> CardHoverTips => [];
    protected abstract IEnumerable<DynamicVar> CardVars { get; }

    public sealed override CardAssetProfile AssetProfile => CardAssetProfile;

    public sealed override IEnumerable<CardKeyword> CanonicalKeywords => CardKeywords;

    protected sealed override IEnumerable<IHoverTip> AdditionalHoverTips =>
        new List<IHoverTip>(BangDreamConst.KeywordMusic.GetModKeywordHoverTips()).Concat(CardHoverTips);

    protected sealed override IEnumerable<DynamicVar> CanonicalVars => CardVars;

    public virtual bool IsInstant { get; set; } = false;

    public virtual PileType WhenStopMoveToPile => BangDreamConst.PileExtraDraw.GetPileType();

    public virtual Task OnStartPerformance(PlayerChoiceContext choiceContext)
    {
        return Task.CompletedTask;
    }

    public virtual Task OnStopPerformance(PlayerChoiceContext choiceContext)
    {
        return Task.CompletedTask;
    }

    protected sealed override PileType GetResultPileTypeForCardPlay()
    {
        return BangDreamConst.PilePerformance.GetPileType();
    }

    protected override PileType GetResultPileTypeForOnTurnEndInHandEffect()
    {
        return BangDreamConst.PileExtraDraw.GetPileType();
    }
}