using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class Pointillism() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.Self;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust,
        BangDreamConst.PerformanceArea
    ];

    protected override IEnumerable<DynamicVar> CardVars => [];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var performanceCards = BangDreamTools.GetPile(BangDreamConst.PerformanceTable, Owner).Cards.ToList();
        var count = performanceCards.Count;

        foreach (var card in performanceCards)
        {
            await CardPileCmd.Add(card, BangDreamConst.ExtraDraw);
        }

        if (count > 0)
        {
            var extraDrawCards = BangDreamTools.GetPile(BangDreamConst.ExtraDraw, Owner).Cards.ToList();
            if (extraDrawCards.Count > 0)
            {
                var selectedCards = await CardSelectCmd.FromSimpleGrid(choiceContext, extraDrawCards,
                    Owner, CardSelectorPrompt.ToHand.GetLimitedPrefs(count, true, true));

                foreach (var selectedCard in selectedCards)
                {
                    await CardPileCmd.Add(selectedCard, PileType.Hand);
                }
            }
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}