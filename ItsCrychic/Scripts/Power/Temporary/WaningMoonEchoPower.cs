using BangDreamLib.Scripts.Powers;
using ItsCrychic.Scripts.Cards.Saki.Attack;
using ItsCrychic.Scripts.Power.Buff;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Power.Temporary;

public class WaningMoonEchoPower : BandTemporaryPowerModel
{
    public override AbstractModel OriginModel => ModelDb.Card<WaningMoonEcho>();

    public override PowerModel InternallyAppliedPower => ModelDb.Power<IncreaseLoudspeakerPower>();
}