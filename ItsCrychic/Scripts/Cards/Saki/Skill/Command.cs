using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class Command() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Common;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Innate,
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Cards.Create(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var selectedCards = await CardSelectCmd.FromCombatPile(choiceContext,
            BangDreamTools.GetPile(BangDreamConst.ExtraDraw, Owner),
            Owner,
            CardSelectorPrompt.ToHand.GetFixedPrefs(DynamicVars.Cards.IntValue)
        );

        foreach (var selectedCard in selectedCards)
        {
            await CardPileCmd.Add(selectedCard, PileType.Hand);
        }
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}