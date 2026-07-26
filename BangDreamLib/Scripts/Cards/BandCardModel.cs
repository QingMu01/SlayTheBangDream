using BangDreamLib.Scripts.Extensions;
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
    private CardAssetProfile? _profile;
    public sealed override IEnumerable<CardKeyword> CanonicalKeywords => CardKeywords;
    protected sealed override IEnumerable<IHoverTip> AdditionalHoverTips => CardHoverTips;
    protected sealed override IEnumerable<DynamicVar> CanonicalVars => CardVars;

    public override CardAssetProfile AssetProfile => _profile ??= new CardAssetProfile
    {
        PortraitPath = GetType().GetCardImg(),
        BetaPortraitPath = GetType().GetCardBateImg()
    };

    protected virtual IEnumerable<CardKeyword> CardKeywords => [];
    protected virtual IEnumerable<IHoverTip> CardHoverTips => [];
    protected virtual IEnumerable<DynamicVar> CardVars => [];
}