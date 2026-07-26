using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Content;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class Face() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None)
{
    private const int TargetLingered = 3;

    public override bool IsInstant => !IsUpgraded;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.Lingered
    ];

    protected override IEnumerable<DynamicVar> CardVars => [];

    public override async Task OnPerform(PlayerChoiceContext choiceContext)
    {
        var current = SecondaryResourceCmd.Get(Owner, BangDreamConst.LingeredResource);
        if (current < TargetLingered)
        {
            await SecondaryResourceCmd.Gain(Owner, BangDreamConst.LingeredResource, TargetLingered - current, this);
        }
        else if (current > TargetLingered)
        {
            await SecondaryResourceCmd.Spend(Owner, BangDreamConst.LingeredResource, current - TargetLingered, this);
        }
    }

    protected override void OnUpgrade()
    {
        this.RequestVisualReload();
    }
}