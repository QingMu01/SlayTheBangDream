using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace ItsCrychic.Scripts.Power.Buff;

public class DivineAscentPower : BandPowerModel, IModifyLingeredHook
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public decimal ModifyLingeredEnergyReduce(decimal amount)
    {
        return 0;
    }
}