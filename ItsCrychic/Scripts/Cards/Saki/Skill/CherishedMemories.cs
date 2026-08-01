using BangDreamLib.Scripts.Extensions;
using ItsCrychic.Scripts.Cards.Token;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class CherishedMemories() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 2;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromCard<MemoryPuzzle>(IsUpgraded)
    ];

    protected override IEnumerable<DynamicVar> CardVars => [QuickVar.Cards.Create(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var handCards = PileType.Hand.GetPile(Owner).Cards.ToList();
        var discardedCount = handCards.Count;

        await CardCmd.Discard(choiceContext, handCards);

        var generatedCards = new List<CardModel>();
        for (var i = 0; i < discardedCount; i++)
        {
            var puzzle = CombatState.CreateCard<MemoryPuzzle>(Owner);
            if (IsUpgraded) CardCmd.Upgrade(puzzle);
            generatedCards.Add(puzzle);
        }

        await CardPileCmd.AddGeneratedCardsToCombat(generatedCards, PileType.Hand, Owner);
    }
}