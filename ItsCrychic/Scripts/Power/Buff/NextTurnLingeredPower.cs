using BangDreamLib.Scripts.Powers;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Power.Buff;

public class NextTurnLingeredPower : BandPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStartLate(CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (Owner.Player == null || !participants.Contains(Owner))
        {
            return;
        }

        await SecondaryResourceCmd.Gain(Owner.Player, BangDreamConst.LingeredResource, Amount, this);
        await PowerCmd.Remove(this);
    }
}
