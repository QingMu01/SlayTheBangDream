using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Power.Buff;

public class InterludePower : BandPowerModel, ICardPerformanceHook
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task OnCardEnterPerformanceArea(CardModel cardModel)
    {
        await CreatureCmd.GainBlock(Owner, new BlockVar(Amount, ValueProp.Unpowered), null);
    }

    public Task OnCardLeavePerformanceArea(CardModel cardModel)
    {
        return Task.CompletedTask;
    }
}