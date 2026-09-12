using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class Ether() : AbstractSakikoMusicCard(CustomRarity, CustomTarget)
{
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<IHoverTip> CardHoverTips
    {
        get
        {
            return ModelDb.AllCards.Where(item => item.Tags.Contains(BangDreamConst.SymbolCard))
                .Select(cardModel => HoverTipFactory.FromCard(cardModel));
        }
    }

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var symbolCards = Owner.PlayerCombatState!.AllCards
            .Concat(BangDreamConst.ExtraDraw.GetPile(Owner).Cards)
            .Concat(BangDreamConst.PerformPile.GetPile(Owner).Cards)
            .Where(card => card.Tags.Contains(BangDreamConst.SymbolCard))
            .Distinct()
            .ToList();

        if (symbolCards.Count > 0)
        {
            var selectedCard = Owner.RunState.Rng.CombatCardSelection.NextItem(symbolCards);
            if (selectedCard != null)
            {
                await CardPileCmd.Add(selectedCard, PileType.Hand);
            }

            return;
        }

        var prototypes = ModelDb.AllCards
            .Where(card => card.Tags.Contains(BangDreamConst.SymbolCard))
            .ToList();
        var prototype = Owner.RunState.Rng.CombatCardGeneration.NextItem(prototypes);
        if (prototype != null)
        {
            var generatedCard = CombatState.CreateCard(prototype, Owner);
            await CardPileCmd.AddGeneratedCardToCombat(generatedCard,
                IsUpgraded ? PileType.Hand : BangDreamConst.ExtraDraw, Owner,
                IsUpgraded ? CardPilePosition.Bottom : CardPilePosition.Random);
        }
    }
}