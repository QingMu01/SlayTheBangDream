using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Features.Rule;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class InYourBlueEyes() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None)
{
    private readonly HashSet<CardPlay> _subsidePlays = [];

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.Perform,
        BangDreamConst.Lingered
    ];

    protected override IEnumerable<DynamicVar> CardVars => [QuickVar.Block.Create(5)];

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (Handle != null && cardPlay.Card.Owner == Owner && LingeredResourcesRule.IsSufficient(this))
        {
            _subsidePlays.Add(cardPlay);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (Handle != null && play.Card.Owner == Owner && play.Card is ISubsideCard &&
            !_subsidePlays.Remove(play))
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3);
    }
}