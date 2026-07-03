using BangDreamLib.Scripts.Relics;
using ItsCrychic.Scripts.Character.RelicPools;
using STS2RitsuLib.Interop.AutoRegistration;

namespace ItsCrychic.Scripts.Relics.Mutsumi;

[RegisterRelic(typeof(MutsumiRelicPool), Inherit = true)]
public abstract class AbstractMutsumiRelic : BandRelicModel
{
}