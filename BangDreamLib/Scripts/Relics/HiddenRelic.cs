using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization;
using STS2RitsuLib.Relics.Visibility;

namespace BangDreamLib.Scripts.Relics;

public abstract class HiddenRelic : BandRelicModel, IModRelicVisibility
{
    public sealed override RelicRarity Rarity => RelicRarity.None;
    public override LocString Title => new("gameplay_ui", "BANG_DREAM_LIB_EMPTY_LOC_STRING");

    public bool IsRelicVisible => false;
}