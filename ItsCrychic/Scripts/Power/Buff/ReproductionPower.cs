using BangDreamInSpire.Scripts.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Power.Buff;

/// <summary>
/// 再生产：本回合每造成过一次伤害，在回合结束时恢复1点生命。
/// 只统计「次数」，不按伤害量累计；回合结束时统一结算并移除自身。
/// </summary>
public class ReproductionPower : BandPowerModel
{
    private int _damageInstanceCount;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer == Owner && result.TotalDamage + result.OverkillDamage > 0)
        {
            _damageInstanceCount++;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner)) return;

        var healAmount = _damageInstanceCount;
        _damageInstanceCount = 0;

        if (healAmount > 0)
        {
            Flash();
            await CreatureCmd.Heal(Owner, healAmount);
        }

        await PowerCmd.Remove(this);
    }
}
