using BangDreamLib.Scripts.Attributes;
using BangDreamLib.Scripts.Relics;
using ItsCrychic.Scripts.Character.RelicPools;
using ItsCrychic.Scripts.Utils;
using STS2RitsuLib.Scaffolding.Content;

namespace ItsCrychic.Scripts.Relics.Sakiko;

[BangDreamPool(typeof(SakikoRelicPool))]
public abstract class AbstractSakikoRelic : BandRelicModel
{
    public override RelicAssetProfile AssetProfile => CrychicConst.DefaultRelicAssetProfile(this);
}