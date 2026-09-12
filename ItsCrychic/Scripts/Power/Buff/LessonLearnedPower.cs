using BangDreamLib.Scripts.Powers;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Power.Buff;

public class LessonLearnedPower : BandPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (Owner.Player != null && participants.Contains(Owner))
        {
            await SecondaryResourceCmd.Gain(Owner.Player, BangDreamConst.LingeredResource, Amount, this);
        }
    }
}