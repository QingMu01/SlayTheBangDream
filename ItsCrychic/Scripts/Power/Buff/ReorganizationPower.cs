using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ItsCrychic.Scripts.Power.Buff;

public class ReorganizationPower : BandPowerModel, ILingeredChangedHook
{
    private const int MaxAmount = 7;

    private int _lingeredEnergyUsed;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    async Task ILingeredChangedHook.AfterLingeredEnergyReduced(Player player, int amount)
    {
        if (player != Owner.Player) return;

        _lingeredEnergyUsed += amount;
        while (_lingeredEnergyUsed >= MaxAmount)
        {
            Flash();
            _lingeredEnergyUsed -= MaxAmount;
            await CardPileCmd.Draw(new BlockingPlayerChoiceContext(), Amount, Owner.Player);
        }
    }
}