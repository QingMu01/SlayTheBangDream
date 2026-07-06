using STS2RitsuLib.Combat.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace BangDreamLib.Scripts.Powers;

[RegisterPower(Inherit = true)]
public abstract class BandTemporaryPowerModel : ModTemporaryPowerTemplate
{
    // protected abstract PowerAssetProfile CardAssetProfile { get; }
}