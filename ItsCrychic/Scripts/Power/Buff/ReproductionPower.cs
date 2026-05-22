using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Power.Buff;

public class ReproductionPower : BandPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner.Player)
        {
            await PowerCmd.Remove(this);
        }
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (dealer == Owner && props.HasFlag(ValueProp.Unpowered) && result.Receiver != Owner &&
            result.TotalDamage + result.OverkillDamage > 0)
        {
            Flash();
            await CreatureCmd.Heal(Owner, result.TotalDamage + result.OverkillDamage);
        }
    }
}