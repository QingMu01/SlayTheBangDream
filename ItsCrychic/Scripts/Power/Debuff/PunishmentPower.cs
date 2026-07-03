using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Power.Debuff;

public class PunishmentPower : BandPowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && !props.HasFlag(ValueProp.Move) &&
            !props.HasFlag(ValueProp.Unpowered | ValueProp.Unblockable | ValueProp.SkipHurtAnim) &&
            result.TotalDamage > 0)
        {
            await CreatureCmd.Damage(choiceContext, target,
                new DamageVar(Amount, ValueProp.Unpowered | ValueProp.Unblockable | ValueProp.SkipHurtAnim), Owner);
        }
    }
}