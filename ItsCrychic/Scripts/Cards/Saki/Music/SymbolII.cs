using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class SymbolIi() : AbstractSakikoMusicCard(CustomRarity, CustomTarget)
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
        QuickVar.Cards.Create("Draw", 3)
    ];

    public override async Task OnStartPerformance(PlayerChoiceContext choiceContext)
    {
        await MusicNoteCmd.FromCard(this, DynamicVars.Repeat.IntValue);
    }

    public override async Task OnStopPerformance(PlayerChoiceContext choiceContext)
    {
        var exhaustPile = PileType.Exhaust.GetPile(Owner);
        if (exhaustPile.Cards.Count > 0)
        {
            CardModel? selectedCard;
            if (IsUpgraded)
            {
                var selectedCards = await CardSelectCmd.FromCombatPile(choiceContext,
                    exhaustPile,
                    Owner,
                    CardSelectorPrompt.ToHand.GetFixedPrefs(1)
                );
                selectedCard = selectedCards.FirstOrDefault();
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