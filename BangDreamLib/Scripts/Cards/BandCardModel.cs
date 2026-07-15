using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Scaffolding.Content;

namespace BangDreamLib.Scripts.Cards;

public abstract class BandCardModel(
    int baseCost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true)
    : ModCardTemplate(baseCost, type, rarity, target, showInCardLibrary)
{
    public sealed override CardAssetProfile AssetProfile => CardAssetProfile;

    public sealed override IEnumerable<CardKeyword> CanonicalKeywords => CardKeywords;
    protected sealed override IEnumerable<IHoverTip> AdditionalHoverTips => CardHoverTips;
    protected sealed override IEnumerable<DynamicVar> CanonicalVars => CardVars;

    protected abstract CardAssetProfile CardAssetProfile { get; }

    protected virtual IEnumerable<CardKeyword> CardKeywords => [];
    protected virtual IEnumerable<IHoverTip> CardHoverTips => [];
    protected virtual IEnumerable<DynamicVar> CardVars => [];
}