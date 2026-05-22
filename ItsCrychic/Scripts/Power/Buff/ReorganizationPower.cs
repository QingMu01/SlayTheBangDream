using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Power.Buff;

public class ReorganizationPower : BandPowerModel, ICardPerformanceHook
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public Task OnCardEnterPerformanceArea(CardModel cardModel)
    {
        return Task.CompletedTask;
    }

    public async Task OnCardLeavePerformanceArea(CardModel cardModel)
    {
        await LingeredCmd.AddLeByPower(this, Amount);
    }
}