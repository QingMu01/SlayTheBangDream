using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Power.Buff;

public class ReorganizationPower : BandPowerModel, ISecondaryResourceHookListener
{
    private const int MaxAmount = 7;

    private int _lingeredEnergyUsed;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task AfterSecondaryResourceChanged(SecondaryResourceChangeContext context)
    {
        if (context.Player != Owner.Player) return;
        if (context.NewAmount < context.OldAmount)
        {
            _lingeredEnergyUsed += context.OldAmount - context.NewAmount;
            while (_lingeredEnergyUsed >= MaxAmount)
            {
                Flash();
                _lingeredEnergyUsed -= MaxAmount;
                await CardPileCmd.Draw(new BlockingPlayerChoiceContext(), Amount, Owner.Player);
            }
        }
    }
}