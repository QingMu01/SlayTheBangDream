using BangDreamLib.Scripts.Interfaces.GameHook;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class STheWay() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None), IMusicNoteModifyHookListener
{
    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
    ];

    protected override IEnumerable<DynamicVar> CardVars => [];



    protected override void OnUpgrade()
    {
        // TODO
    }
}
