using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Power.Buff;

public class GrittedTeethPower : BandPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(7m, ValueProp.Move | ValueProp.Unpowered),
        new IntVar("Resonance", 3),
        new EnergyVar(1)
    ];

    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        await PowerCmd.Decrement(this);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner.Player)
        {
            Flash();
            await PlayerCmd.LoseEnergy(1, Owner.Player);
            await CreatureCmd.GainBlock(Owner, DynamicVars.Block, null);
            await LingeredCmd.AddLeByPower(this, DynamicVars["Resonance"].IntValue);
        }
    }
}