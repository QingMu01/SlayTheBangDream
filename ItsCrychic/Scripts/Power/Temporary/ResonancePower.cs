using BangDreamLib.Scripts.Powers;
using ItsCrychic.Scripts.Cards.Saki.Skill;
using ItsCrychic.Scripts.Power.Buff;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Power.Temporary;

public class ResonancePower : BandTemporaryPowerModel
{
    public override AbstractModel OriginModel => ModelDb.Card<Resonance>();

    public override PowerModel InternallyAppliedPower => ModelDb.Power<IncreaseVolumePower>();
}