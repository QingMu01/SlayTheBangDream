using BangDreamInSpire.Scripts.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Power.Debuff;

/// <summary>本回合受攻击时额外失去生命，格挡不豁免；玩家回合结束时移除。</summary>
public class PunishmentPower : BandPowerModel
{
    private bool _isTriggering;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && !_isTriggering && props.IsPoweredAttack())
        {
            _isTriggering = true;
            try
            {
                await CreatureCmd.Damage(choiceContext, target,
                    new DamageVar(Amount, ValueProp.Unpowered | ValueProp.Unblockable | ValueProp.SkipHurtAnim),
                    Owner);
            }
            finally
            {
                _isTriggering = false;
            }
        }
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player)
        {
            await PowerCmd.Remove(this);
        }
    }
}
