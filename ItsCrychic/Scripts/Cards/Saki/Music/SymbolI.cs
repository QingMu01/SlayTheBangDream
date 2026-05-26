using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Keywords;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class SymbolI() : AbstractSakikoMusicCard(CustomRarity, CustomTarget)
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
        QuickVar.Block.Create(15),
        QuickVar.Cards.Create(1)
    ];

    public override async Task OnStartPerformance(PlayerChoiceContext choiceContext)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, null);
    }

    public override async Task OnStopPerformance(PlayerChoiceContext choiceContext)
    {
        var discardPile = Owner.PlayerCombatState!.DiscardPile;
        if (discardPile.Cards.Count > 0)
        {
            CardModel? selectedCard;
            if (IsUpgraded)
            {
                var simpleGridSelected = await CardSelectCmd.FromSimpleGrid(choiceContext,
                    discardPile.Cards, Owner, CardSelectorPrompt.ToHand.GetFixedPrefs(DynamicVars.Cards.IntValue));
                selectedCard = simpleGridSelected.FirstOrDefault();
            }
            else
            {
                selectedCard = Owner.RunState.Rng.CombatCardSelection.NextItem(discardPile.Cards);
            }

            if (selectedCard != null)
            {
                await CardPileCmd.Add(selectedCard, PileType.Hand);
            }
        }
    }
}