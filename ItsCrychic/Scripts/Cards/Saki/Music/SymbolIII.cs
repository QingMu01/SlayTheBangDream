using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class SymbolIii() : AbstractSakikoMusicCard(CustomRarity, CustomTarget)
{
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.Perform,
        BangDreamConst.PerformArea
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Block.Create(15)
    ];

    public override async Task OnStartPerform(PlayerChoiceContext choiceContext)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, null);
    }

    public override async Task OnStopPerform(PlayerChoiceContext choiceContext)
    {
        var discardPile = PileType.Discard.GetPile(Owner);
        if (discardPile.Cards.Count > 0)
        {
            CardModel? selectedCard;
            if (IsUpgraded)
            {
                var selectedCards = await CardSelectCmd.FromCombatPile(choiceContext,
                    discardPile,
                    Owner,
                    CardSelectorPrompt.ToHand.GetFixedPrefs(1)
                );
                selectedCard = selectedCards.FirstOrDefault();
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