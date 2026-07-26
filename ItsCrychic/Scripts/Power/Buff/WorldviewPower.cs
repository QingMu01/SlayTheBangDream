using BangDreamLib.Scripts.Powers;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ItsCrychic.Scripts.Power.Buff;

public class WorldviewPower : BandPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player || Owner.CombatState == null) return;


        var musicCards = CardFactory.FilterForCombat(BangDreamTools.GetCharacterExtraCards(player, true)).ToList();

        if (Amount > 0 && musicCards.Count > 0)
        {
            var generatedCards = Enumerable.Range(0, Amount)
                .Select(_ => Owner.Player.RunState.Rng.CombatCardGeneration.NextItem(musicCards))
                .Where(card => card != null)
                .Select(card =>
                {
                    var generatedCard = Owner.CombatState.CreateCard(card!, Owner.Player);
                    generatedCard.AddKeyword(CardKeyword.Exhaust);
                    return generatedCard;
                })
                .ToList();
            CardCmd.PreviewCardPileAdd(await CardPileCmd.AddGeneratedCardsToCombat(generatedCards,
                BangDreamConst.PerformPile, Owner.Player));
        }
    }
}
