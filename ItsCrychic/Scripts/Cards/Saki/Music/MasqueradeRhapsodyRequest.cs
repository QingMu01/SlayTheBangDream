using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Cards.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class MasqueradeRhapsodyRequest() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None),
    IMusicNoteModifyHook
{
    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.Perform,
        BangDreamConst.MusicNote
    ];

    protected override IEnumerable<DynamicVar> CardVars => [ModCardVars.Int("Increase", 1)];

    public decimal ModifyMusicNoteDamageAdditive(Creature? target, decimal amount, Creature? dealer,
        AbstractModel? source)
    {
        return Handle != null && dealer == Owner.Creature ? DynamicVars["Increase"].BaseValue : 0;
    }

    public decimal ModifyMusicNoteShotCount(decimal amount, Creature? dealer, AbstractModel? source)
    {
        return Handle != null && dealer == Owner.Creature ? amount + DynamicVars["Increase"].BaseValue : amount;
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Increase"].UpgradeValueBy(1);
    }
}
