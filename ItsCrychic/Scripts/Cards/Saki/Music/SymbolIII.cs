using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Keywords;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class SymbolIii() : AbstractSakikoMusicCard(CustomRarity, CustomTarget)
{
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.KeywordPerformance.GetModCardKeyword(),
        BangDreamConst.KeywordPerformanceArea.GetModCardKeyword()
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Cards.Create(3),
        QuickVar.Cards.Create("SelectCard", 1)
    ];

    public override async Task OnStartPerformance(PlayerChoiceContext choiceContext)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }

    public override async Task OnStopPerformance(PlayerChoiceContext choiceContext)
    {
        var exhaustPile = Owner.PlayerCombatState!.ExhaustPile;
        if (exhaustPile.Cards.Count > 0)
        {
            CardModel? selectedCard;
            if (IsUpgraded)
            {
                var simpleGridSelected = await CardSelectCmd.FromSimpleGrid(choiceContext,
                    exhaustPile.Cards, Owner,
                    CardSelectorPrompt.ToHand.GetFixedPrefs(DynamicVars["SelectCard"].IntValue));
                selectedCard = simpleGridSelected.FirstOrDefault();
            }
            else
            {
                selectedCard = Owner.RunState.Rng.CombatCardSelection.NextItem(exhaustPile.Cards);
            }

            if (selectedCard != null)
            {
                await CardPileCmd.Add(selectedCard, PileType.Hand);
            }
        }
    }
}