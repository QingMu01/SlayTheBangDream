using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class HaneokaIndifference() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.None;

    public override bool GainsBlock => true;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Ethereal
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Block.Create(6),
        QuickVar.Cards.Create(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);

        var pileCards = PileType.Draw.GetPile(Owner).Cards.ToList();
        if (IsUpgraded)
        {
            pileCards.AddRange(BangDreamConst.ExtraDeck.GetPile(Owner).Cards.ToList());
        }

        var selectedCards = await CardSelectCmd.FromSimpleGrid(choiceContext,
            pileCards,
            Owner,
            CardSelectorPrompt.ToHand.GetFixedPrefs(DynamicVars.Cards.IntValue)
        );

        foreach (var selectedCard in selectedCards)
        {
            var cloneCard = CombatState.RunState.CloneCard(selectedCard.ToMutable());
            await CardPileCmd.AddGeneratedCardToCombat(cloneCard, PileType.Hand, Owner);
        }
    }
}