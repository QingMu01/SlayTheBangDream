using BangDreamLib.Scripts.Relics;
using ItsCrychic.Scripts.Character.RelicPools;
using STS2RitsuLib.Interop.AutoRegistration;

namespace ItsCrychic.Scripts.Relics.Sakiko;

[RegisterRelic(typeof(SakikoRelicPool), Inherit = true)]
public abstract class AbstractSakikoRelic : BandRelicModel
{
}