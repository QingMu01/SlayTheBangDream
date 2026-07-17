using BangDreamLib.Scripts.Powers;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Power.Debuff;

public class MutedLingeredPower : BandPowerModel, ISecondaryResourceHookListener
{
    private bool _skipCurrentTurnEnd = true;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public bool ShouldGainSecondaryResource(SecondaryResourceContext context, decimal amount)
    {
        return context.Player != Owner.Player || !context.Definition.Id.Equals(BangDreamConst.LingeredResource);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner)) return;

        if (_skipCurrentTurnEnd)
        {
            _skipCurrentTurnEnd = false;
            return;
        }

        await PowerCmd.TickDownDuration(this);
    }
}
