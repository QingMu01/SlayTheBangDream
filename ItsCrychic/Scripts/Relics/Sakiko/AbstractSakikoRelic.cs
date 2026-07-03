using BangDreamLib.Scripts.Relics;
using ItsCrychic.Scripts.Character.RelicPools;
using ItsCrychic.Scripts.Utils;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ItsCrychic.Scripts.Relics.Sakiko;

[RegisterRelic(typeof(SakikoRelicPool), Inherit = true)]
public abstract class AbstractSakikoRelic : BandRelicModel
{
    public override RelicAssetProfile AssetProfile => CrychicConst.DefaultRelicAssetProfile(this);
}