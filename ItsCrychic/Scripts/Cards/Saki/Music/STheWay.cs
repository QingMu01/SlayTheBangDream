using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Cards.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class STheWay() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None), IMusicNoteModifyHook,
    IMusicNotePlayedHook
{
    protected override IEnumerable<CardKeyword> CardKeywords => [BangDreamConst.Perform];

    protected override IEnumerable<DynamicVar> CardVars => [ModCardVars.Int("Bounce", 1)];

    public decimal ModifyMusicNoteBounceCount(decimal amount, Creature? dealer, AbstractModel? source)
    {
        return Handle != null && dealer == Owner.Creature ? amount + DynamicVars["Bounce"].BaseValue : amount;
    }

    public Task OnMusicNoteSpawn(VfxContext context, Player dealer)
    {
        if (Handle != null && dealer == Owner && !context.Get<bool>("IsPrototype"))
        {
            FlashInArea();
        }

        return Task.CompletedTask;
    }


    protected override void OnUpgrade()
    {
        DynamicVars["Bounce"].UpgradeValueBy(1);
    }
}