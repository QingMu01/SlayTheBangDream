using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class Divine() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None)
{
    protected override IEnumerable<CardKeyword> CardKeywords => [BangDreamConst.Perform];

    protected override IEnumerable<DynamicVar> CardVars => [ModCardVars.Int("Notes", 2)];

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Handle != null && cardPlay.Card.Type == CardType.Attack)
        {
            await MusicNoteCmd.FromCard(this, DynamicVars["Notes"].IntValue);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Notes"].UpgradeValueBy(1);
    }
}