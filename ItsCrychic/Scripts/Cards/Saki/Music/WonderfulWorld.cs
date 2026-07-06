using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class WonderfulWorld() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None)
{
    private bool _playedAttack;

    protected override IEnumerable<CardKeyword> CardKeywords => [BangDreamConst.Perform];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new PowerVar<VigorPower>("Vigor", 8)
    ];

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (Handle != null && play.Card.Owner == Owner && play.Card.Type == CardType.Attack)
        {
            _playedAttack = true;
        }

        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Handle != null && side == CombatSide.Player && participants.Contains(Owner.Creature) && !_playedAttack)
        {
            await PowerCmd.Apply<VigorPower>(choiceContext, Owner.Creature, DynamicVars["Vigor"].IntValue,
                Owner.Creature, this);
        }
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner)
        {
            _playedAttack = false;
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Vigor"].UpgradeValueBy(4);
    }
}