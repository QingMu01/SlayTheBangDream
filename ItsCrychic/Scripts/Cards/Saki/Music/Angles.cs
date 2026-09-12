using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class Angles() : AbstractSakikoMusicCard(CardRarity.Rare, TargetType.None)
{
    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Cards.Create(1)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        ArgumentNullException.ThrowIfNull(CombatState);
        var attackCards = Owner.Character.CardPool.AllCards
            .Where(card => card is { Type: CardType.Attack, Rarity: CardRarity.Rare })
            .ToList();
        var candidates = CardFactory.FilterForCombat(attackCards);

        var selectedCard = Owner.RunState.Rng.CombatCardGeneration.NextItem(candidates);

        if (selectedCard != null)
        {
            var generatedCard = CombatState.CreateCard(selectedCard, Owner);
            if (IsUpgraded)
            {
                CardCmd.Upgrade(generatedCard);
            }

            await CardCmd.AutoPlay(choiceContext, generatedCard, null);
        }
    }
}