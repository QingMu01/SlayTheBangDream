using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace BangDreamLib.Scripts.Powers;

[RegisterPower(Inherit = true)]
public abstract class BandPowerModel : ModPowerTemplate
{
    protected sealed override IEnumerable<IHoverTip> AdditionalHoverTips => PowerHoverTips;
    protected sealed override IEnumerable<DynamicVar> CanonicalVars => PowerVars;

    // protected virtual PowerAssetProfile CardAssetProfile { get; }

    protected virtual IEnumerable<IHoverTip> PowerHoverTips => [];
    protected virtual IEnumerable<DynamicVar> PowerVars => [];
}