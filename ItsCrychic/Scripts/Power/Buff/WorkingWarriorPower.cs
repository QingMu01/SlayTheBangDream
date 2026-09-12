using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Rooms;

namespace ItsCrychic.Scripts.Power.Buff;

/// <summary>
/// 战斗胜利后结算金币奖励。
/// </summary>
public class WorkingWarriorPower : BandPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (Owner.Player == null) return;

        await PlayerCmd.GainGold(Amount, Owner.Player);
        await PowerCmd.Remove(this);
    }
}
