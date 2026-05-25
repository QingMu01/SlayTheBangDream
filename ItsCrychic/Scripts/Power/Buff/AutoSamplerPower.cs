using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Powers;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Power.Buff;

public class AutoSamplerPower : BandPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && participants.Contains(Owner) && Owner.Player != null)
        {
            var playerData = Owner.Player.AttachedData();
            var lingeredEnergy = playerData.LingeredEnergy.Counter;

            if (lingeredEnergy > 0)
            {
                Flash();
                await CreatureCmd.GainBlock(Owner,
                    new BlockVar(Amount * lingeredEnergy, ValueProp.Move | ValueProp.Unpowered), null);
                await LingeredCmd.JustReduce(Owner.Player, lingeredEnergy);
            }
        }
    }
}