using BangDreamInSpire.Scripts.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Power.Buff;

/// <summary>
/// 距离感：本回合所有单位的攻击伤害为 0（敌我双方都只免疫 <see cref="ValueProp.Move" /> 伤害，
/// 音符 / 余音 / 能力等非攻击伤害不受影响）。挂在出牌者身上的临时能力，敌方回合结束时移除，
/// 因此覆盖"本回合 + 随后的敌方回合"这一整轮。
/// </summary>
public class DistanceNullifyPower : BandPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        // 攻击伤害（含攻击牌与怪物攻击）归零；非攻击伤害保持原样。
        return props.HasFlag(ValueProp.Move) ? 0m : 1m;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy)
        {
            await PowerCmd.Remove(this);
        }
    }
}
