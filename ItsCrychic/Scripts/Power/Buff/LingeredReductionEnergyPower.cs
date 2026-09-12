using BangDreamLib.Scripts.Powers;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Power.Buff;

public class LingeredReductionEnergyPower : BandPowerModel, ISecondaryResourceHookListener
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task AfterSecondaryResourceChanged(SecondaryResourceChangeContext context)
    {
        if (context.Player == Owner.Player &&
            context.Definition.Id.Equals(BangDreamConst.LingeredResource) &&
            context.NewAmount < context.OldAmount)
        {
            await PlayerCmd.GainEnergy(Amount, context.Player);
            await PowerCmd.Remove(this);
        }
    }
}
