using BangDreamLib.Scripts.Powers;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ItsCrychic.Scripts.Power.Buff;

public class CelestialRotationPower : BandPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player) return;

        var performPile = BangDreamConst.PerformPile.GetPile(player);
        if (performPile.Cards.Count == 0) return;

        var selectedCards = await CardSelectCmd.FromCombatPile(choiceContext,
            performPile,
            player,
            CardSelectorPrompt.ToHand.GetFixedPrefs(Math.Min(Amount, performPile.Cards.Count)));

        foreach (var selectedCard in selectedCards)
        {
            await CardPileCmd.Add(selectedCard, PileType.Hand);
        }
    }
}
