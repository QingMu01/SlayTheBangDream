using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class BlackBirthday() : AbstractSakikoMusicCard(CardRarity.Rare, TargetType.None)
{
    public override async Task OnPerform(PlayerChoiceContext choiceContext)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var cardModels = CardFactory.FilterForCombat(
            ModelDb.CardPool<ColorlessCardPool>()
                .GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
        );
        var prototype = Owner.RunState.Rng.CombatCardSelection.NextItem(cardModels);
        if (prototype != null)
        {
            var generatedCard = CombatState.CreateCard(prototype, Owner);
            if (IsUpgraded)
            {
                generatedCard.EnergyCost.AddThisTurn(-1, true);
            }

            await CardPileCmd.AddGeneratedCardToCombat(generatedCard, PileType.Hand, Owner);
        }
    }
}