using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class SymbolIv() : AbstractSakikoMusicCard(CustomRarity, CustomTarget)
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
        QuickVar.Energy.Create(2)
    ];

    public override async Task OnStartPerform(PlayerChoiceContext choiceContext)
    {
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
    }

    public override async Task OnStopPerform(PlayerChoiceContext choiceContext)
    {
        var extraDrawPile = BangDreamConst.ExtraDraw.GetPile(Owner);
        if (extraDrawPile.Cards.Count > 0)
        {
            CardModel? selectedCard;
            if (IsUpgraded)
            {
                var selectedCards = await CardSelectCmd.FromCombatPile(choiceContext,
                    extraDrawPile,
                    Owner,
                    CardSelectorPrompt.ToHand.GetFixedPrefs(1)
                );
                selectedCard = selectedCards.FirstOrDefault();
            }
            else
            {
                selectedCard = Owner.RunState.Rng.CombatCardSelection.NextItem(extraDrawPile.Cards);
            }

            if (selectedCard != null)
            {
                await CardPileCmd.Add(selectedCard, PileType.Hand);
            }
        }
    }
}