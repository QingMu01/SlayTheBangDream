using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class Enfeoffment() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Innate,
        CardKeyword.Exhaust
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var selectedCard = (await CardSelectCmd.FromCombatPile(choiceContext,
            PileType.Draw.GetPile(Owner), Owner,
            CardSelectorPrompt.ToHand.GetFixedPrefs(1))).FirstOrDefault();
        if (selectedCard == null)
        {
            return;
        }

        if (selectedCard.IsUpgradable)
        {
            CardCmd.Upgrade(selectedCard);
        }

        CardCmd.ApplyKeyword(selectedCard, CardKeyword.Retain);
        await CardPileCmd.Add(selectedCard, PileType.Hand);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
