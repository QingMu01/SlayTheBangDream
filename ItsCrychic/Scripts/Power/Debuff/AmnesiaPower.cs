using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Powers;
using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Cards.Token;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace ItsCrychic.Scripts.Power.Debuff;

public class AmnesiaPower : BandPowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<IHoverTip> PowerHoverTips =>
    [
        HoverTipFactory.FromCard<Residue>()
    ];

    public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player || cardPlay.Card.Type != CardType.Attack ||
            Owner.Player == null || Owner.CombatState == null) return;

        var manager = Owner.Player.AttachedData().PerformManager;

        if (manager.PerformPile.Cards.Count == 0 || manager.PerformPile.Cards.Any(card => card is not Residue))
        {
            var showAdd = new List<CardPileAddResult>();
            for (var i = 0; i < manager.Capacity; i++)
            {
                var residue = Owner.CombatState.CreateCard<Residue>(Owner.Player);
                var addResult =
                    await CardPileCmd.AddGeneratedCardToCombat(residue, BangDreamConst.PerformPile, Owner.Player);
                showAdd.Add(addResult);
            }

            CardCmd.PreviewCardPileAdd(showAdd);
        }
    }
}