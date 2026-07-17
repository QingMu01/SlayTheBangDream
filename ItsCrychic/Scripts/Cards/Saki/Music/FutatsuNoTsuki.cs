using BangDreamLib.Scripts.Interfaces.GameHook;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class FutatsuNoTsuki() : AbstractSakikoMusicCard(CardRarity.Rare, TargetType.None), IPerformHookListener
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
