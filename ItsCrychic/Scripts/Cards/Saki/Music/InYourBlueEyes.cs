using BangDreamLib.Scripts.Extensions;
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
        BangDreamConst.Performance,
        BangDreamConst.Linger
    ];

    protected override IEnumerable<DynamicVar> CardVars => [QuickVar.Block.Create(5)];

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (Handle != null && cardPlay.Card.Owner == Owner &&
            cardPlay.Card is ISubsideCard { CanSubside: true })
        {
            _subsidePlays.Add(cardPlay);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Handle != null && cardPlay.Card.Owner == Owner && cardPlay.Card is ISubsideCard &&
            !_subsidePlays.Remove(cardPlay))
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3);
    }
}
