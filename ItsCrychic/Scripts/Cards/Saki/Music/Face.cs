using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class Face() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None), ISecondaryResourceHookListener
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
