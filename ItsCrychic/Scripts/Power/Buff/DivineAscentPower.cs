using BangDreamLib.Scripts.Powers;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Power.Buff;

public class DivineAscentPower : BandPowerModel, ISecondaryResourceHookListener
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public decimal ModifySecondaryResourceCost(SecondaryResourceCostContext context, decimal cost)
    {
        return context.Definition.Id.Equals(BangDreamConst.LingeredResource) ? 0m : cost;
    }
}