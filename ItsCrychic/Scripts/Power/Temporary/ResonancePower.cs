using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Power.Temporary;

public class ResonancePower : BandPowerModel, IMusicNoteModifyHookListener
{
    private bool _isActive;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public decimal CaptureMusicNoteDamageAdditive(Creature? dealer, AbstractModel? source)
    {
        if (dealer != Owner) return 0m;

        _isActive = true;
        return Amount;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (_isActive)
        {
            await PowerCmd.Remove(this);
        }
    }
}
