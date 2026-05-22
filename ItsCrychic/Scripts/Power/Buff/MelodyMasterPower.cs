using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Power.Buff;

public class MelodyMasterPower : BandPowerModel, ICardPerformanceHook
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public async Task OnCardEnterPerformanceArea(CardModel cardModel)
    {
        await Task.CompletedTask;
    }

    public Task OnCardLeavePerformanceArea(CardModel cardModel)
    {
        if (cardModel is not IPerformanceCard && cardModel.Owner == Owner.Player)
        {
            Flash();
            cardModel.GiveSingleTurnSly();
        }

        return Task.CompletedTask;
    }
}