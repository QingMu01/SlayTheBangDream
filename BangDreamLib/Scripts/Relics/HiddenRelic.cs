using MegaCrit.Sts2.Core.Entities.Relics;

namespace BangDreamLib.Scripts.Relics;

public abstract class HiddenRelic : BandRelicModel
{
    public sealed override RelicRarity Rarity => RelicRarity.None;
}