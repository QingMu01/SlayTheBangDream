using BangDreamLib.Scripts.Extensions;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class Sophie() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None)
{
    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Cards.Create(1)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext)
    {
        ArgumentNullException.ThrowIfNull(CombatState);
        var powerCards = Owner.Character.CardPool.AllCards.Where(card => card.Type == CardType.Power).ToList();
        var candidates = CardFactory.FilterForCombat(powerCards);
        var selectedCard = Owner.RunState.Rng.CombatCardGeneration.NextItem(candidates);
        if (selectedCard != null)
        {
            var generatedCard = CombatState.CreateCard(selectedCard, Owner);
            generatedCard.AddKeyword(CardKeyword.Ethereal);
            if (IsUpgraded)
            {
                generatedCard.EnergyCost.AddThisTurn(-1, true);
            }

            await CardPileCmd.AddGeneratedCardToCombat(generatedCard, PileType.Hand, Owner);
        }
    }
}