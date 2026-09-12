using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using ItsCrychic.Scripts.Power.Buff;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Content;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class Face() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None)
{
    public override bool IsInstant => true;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.Lingered
    ];

    protected override IEnumerable<DynamicVar> CardVars => [];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        while (SecondaryResourceCmd.Get(Owner, BangDreamConst.LingeredResource) > 0)
        {
            await SecondaryResourceCmd.Spend(Owner, BangDreamConst.LingeredResource, 1, this);
        }

        if (IsUpgraded)
        {
            await PowerCmd.Apply<NextTurnLingeredPower>(choiceContext, Owner.Creature, 1,
                Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        this.RequestVisualReload();
    }
}