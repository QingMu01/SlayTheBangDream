using BangDreamLib.Scripts.Powers;
using ItsCrychic.Scripts.Cards.Saki.Attack;
using ItsCrychic.Scripts.Power.Buff;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Power.Temporary;

public class WaningMoonEchoPower : BandTemporaryPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;
    
    public override AbstractModel OriginModel => ModelDb.Card<WaningMoonEcho>();

    public override PowerModel InternallyAppliedPower => ModelDb.Power<IncreaseLoudspeakerPower>();
}