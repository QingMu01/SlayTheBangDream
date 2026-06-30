using BangDreamLib.Scripts.Powers;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Power.Debuff;

public class ComposeMusicPower : BandPowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner.Player)
        {
            await PowerCmd.Remove(this);
        }
    }

    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(CardModel card,
        bool isAutoPlay,
        ResourceInfo resources, PileType pileType, CardPilePosition position)
    {
        return card.Owner.Creature == Owner && pileType == PileType.Discard
            ? (BangDreamConst.ExtraDraw, CardPilePosition.Bottom)
            : base.ModifyCardPlayResultPileTypeAndPosition(card, isAutoPlay, resources, pileType, position);
    }
}