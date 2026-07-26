using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class Sophie() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None)
{
    protected override IEnumerable<DynamicVar> CardVars => [];

    public override async Task OnPerform(PlayerChoiceContext choiceContext)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var powers = IsUpgraded
            ? ModelDb.AllCards.Where(card => card.Type == CardType.Power)
            : Owner.Character.CardPool.AllCards.Where(card => card.Type == CardType.Power);
        var candidates = CardFactory.FilterForCombat(powers);
        var prototype = Owner.RunState.Rng.CombatCardGeneration.NextItem(candidates);
        if (prototype == null) return;

        var generatedCard = CombatState.CreateCard(prototype, Owner);
        generatedCard.AddKeyword(CardKeyword.Ethereal);
        await CardPileCmd.AddGeneratedCardToCombat(generatedCard, PileType.Hand, Owner);
    }

    protected override void OnUpgrade()
    {
    }
}
