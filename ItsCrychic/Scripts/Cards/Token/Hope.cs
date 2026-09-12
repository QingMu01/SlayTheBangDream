using BangDreamLib.Scripts.Cards;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2RitsuLib.Interop.AutoRegistration;

namespace ItsCrychic.Scripts.Cards.Token;

[RegisterCard(typeof(TokenCardPool))]
public class Hope() : BandCardModel(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Token;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Cards.Create(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var selectedCards = await CardSelectCmd.FromCombatPile(choiceContext,
            PileType.Draw.GetPile(Owner), Owner,
            CardSelectorPrompt.ToHand.GetFixedPrefs(DynamicVars.Cards.IntValue));

        foreach (var selectedCard in selectedCards)
        {
            await CardPileCmd.Add(selectedCard, PileType.Hand);
        }
    }

    protected override void OnUpgrade()
    {
    }
}
