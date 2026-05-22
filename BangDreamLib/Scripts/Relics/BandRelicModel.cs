using BangDreamLib.Scripts.Extensions;
using STS2RitsuLib.Scaffolding.Content;

namespace BangDreamLib.Scripts.Relics;

public abstract class BandRelicModel : ModRelicTemplate
{
    public override RelicAssetProfile AssetProfile => new(
        IconPath: GetType().Name.GetRelicImg(),
        IconOutlinePath: GetType().Name.GetRelicImg(),
        BigIconPath: GetType().Name.GetBigRelicImg()
    );
}