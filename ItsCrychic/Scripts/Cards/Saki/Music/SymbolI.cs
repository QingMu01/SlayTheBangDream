using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class SymbolI() : AbstractSakikoMusicCard(CustomRarity, CustomTarget)
{
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.None;

    
    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromPower<VigorPower>()
    ];

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.Perform,
        BangDreamConst.PerformArea
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new PowerVar<VigorPower>(5)
    ];

    public override async Task OnStartPerform(PlayerChoiceContext choiceContext)
    {
        await PowerCmd.Apply<VigorPower>(choiceContext, Owner.Creature, DynamicVars["VigorPower"].IntValue,
            Owner.Creature, this);
    }

    public override async Task OnStopPerform(PlayerChoiceContext choiceContext)
    {
        var drawPile = PileType.Draw.GetPile(Owner);
        if (drawPile.Cards.Count > 0)
        {
            CardModel? selectedCard;
            if (IsUpgraded)
            {
                var selectedCards = await CardSelectCmd.FromCombatPile(choiceContext,
                    drawPile,
                    Owner,
                    CardSelectorPrompt.ToHand.GetFixedPrefs(1)
                );
                selectedCard = selectedCards.FirstOrDefault();
            }
            else
            {
                selectedCard = Owner.RunState.Rng.CombatCardSelection.NextItem(drawPile.Cards);
            }

            if (selectedCard != null)
            {
                await CardPileCmd.Add(selectedCard, PileType.Hand);
            }
        }
    }
}