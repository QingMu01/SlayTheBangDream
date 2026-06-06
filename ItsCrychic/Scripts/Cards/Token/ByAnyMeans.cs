using BangDreamLib.Scripts.Attributes;
using BangDreamLib.Scripts.Cards;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2RitsuLib.Scaffolding.Content;

namespace ItsCrychic.Scripts.Cards.Token;

[BangDreamPool(typeof(TokenCardPool))]
public class ByAnyMeans() : BandCardModel(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Token;
    private const TargetType CustomTarget = TargetType.None;

    protected override CardAssetProfile CardAssetProfile => CrychicConst.DefaultCardAssetProfile(this);

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Cards.Create(1),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(Owner.PlayerCombatState);

        var drawPileCards = Owner.PlayerCombatState.DrawPile.Cards;
        if (drawPileCards.Count > 0)
        {
            var selectedCards = await CardSelectCmd.FromSimpleGrid(choiceContext, drawPileCards, Owner,
                CardSelectorPrompt.ToPlay.GetFixedPrefs(DynamicVars.Cards.IntValue));

            foreach (var selectedCard in selectedCards)
            {
                selectedCard.ExhaustOnNextPlay = true;
                await CardCmd.AutoPlay(choiceContext, selectedCard, null);
            }
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}