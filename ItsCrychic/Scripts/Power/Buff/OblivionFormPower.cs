using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Power.Buff;

public class OblivionFormPower : BandPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer == Owner && props.IsPoweredAttack() &&
            cardSource is { Type: CardType.Attack } &&
            Owner.Player != null &&
            Owner.Player.AttachedData().PerformManager.PerformancePile.Cards.Count == 0)
        {
            return Amount;
        }

        return 1m;
    }
}
