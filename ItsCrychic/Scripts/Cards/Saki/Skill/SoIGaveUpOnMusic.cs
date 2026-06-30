using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class SoIGaveUpOnMusic() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.None;

    protected override bool IsPlayable => IsDupe || Owner.AttachedData().PerformanceManager.Capacity > 0;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust,
        BangDreamConst.PerformanceArea
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Cards.Create(1),
        ModCardVars.Int("Cost", 1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var selectedCards = await CardSelectCmd.FromCombatPile(choiceContext,
            PileType.Draw.GetPile(Owner),
            Owner,
            CardSelectorPrompt.ToPlay.GetFixedPrefs(DynamicVars.Cards.IntValue)
        );

        foreach (var selectedCard in selectedCards)
        {
            selectedCard.ExhaustOnNextPlay = true;
            await CardCmd.AutoPlay(choiceContext, selectedCard, null);
        }
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card == this && cardPlay.PlayIndex == 0 && !IsDupe)
        {
            Owner.AttachedData().PerformanceManager.ReduceCapacity(DynamicVars["Cost"].IntValue);
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}