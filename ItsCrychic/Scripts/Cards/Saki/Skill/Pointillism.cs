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
        BangDreamConst.PerformArea
    ];

    protected override IEnumerable<DynamicVar> CardVars => [];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var performanceCards = BangDreamTools.GetPile(BangDreamConst.PerformPile, Owner).Cards.ToList();
        var performanceItemCount = performanceCards.Count;
        if (performanceItemCount > 0)
        {
            foreach (var card in performanceCards)
            {
                await CardPileCmd.Add(card, BangDreamConst.ExtraDraw);
            }

            var selectedCards = await CardSelectCmd.FromCombatPile(choiceContext,
                BangDreamTools.GetPile(BangDreamConst.ExtraDraw, Owner),
                Owner,
                CardSelectorPrompt.ToHand.GetLimitedPrefs(performanceItemCount, true, true)
            );

            foreach (var selectedCard in selectedCards)
            {
                await CardPileCmd.Add(selectedCard, PileType.Hand);
            }
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}