using BangDreamLib.Scripts.Powers;
using ItsCrychic.Scripts.Cards.Saki.Skill;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ItsCrychic.Scripts.Power.Temporary;

public class DistancePower : BandTemporaryPowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override AbstractModel OriginModel => ModelDb.Card<Distance>();

    protected override bool IsPositive => false;

    public override PowerModel InternallyAppliedPower => ModelDb.Power<StrengthPower>();
}