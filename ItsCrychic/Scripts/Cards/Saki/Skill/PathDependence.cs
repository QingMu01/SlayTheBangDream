using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class PathDependence() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CardVars => [];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var selectedCards = await CardSelectCmd.FromHand(choiceContext, Owner,
            CardSelectorPrompt.ToExtraDraw.GetUnlimitedPrefs(),
            _ => true, this);

        foreach (var selectedCard in selectedCards)
        {
            await CardPileCmd.Add(selectedCard, BangDreamConst.ExtraDraw, CardPilePosition.Top);
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}