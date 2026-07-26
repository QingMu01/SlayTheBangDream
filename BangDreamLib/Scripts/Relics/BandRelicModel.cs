using BangDreamLib.Scripts.Extensions;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Scaffolding.Content;

namespace BangDreamLib.Scripts.Relics;

public abstract class BandRelicModel : ModRelicTemplate
{
    protected sealed override IEnumerable<IHoverTip> AdditionalHoverTips => RelicHoverTips;
    protected sealed override IEnumerable<DynamicVar> CanonicalVars => RelicVars;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: GetType().GetRelicImg(),
        IconOutlinePath: GetType().GetRelicImg(),
        BigIconPath: GetType().GetBigRelicImg()
    );

    protected virtual IEnumerable<IHoverTip> RelicHoverTips => [];
    protected virtual IEnumerable<DynamicVar> RelicVars => [];
}