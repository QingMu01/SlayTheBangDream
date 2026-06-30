using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class ObliviousPrerogative()
    : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), ISubsideCard
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.None;

    public int LingeredEnergyCost => 5;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Cards.Create(1),
        QuickVar.Repeat.Create(2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        if (!((ISubsideCard)this).CanSubside)
        {
            var selectedCards = await CardSelectCmd.FromCombatPile(choiceContext,
                PileType.Hand.GetPile(Owner),
                Owner,
                CardSelectorPrompt.ToPlay.GetFixedPrefs(DynamicVars.Cards.IntValue)
            );

            foreach (var selectedCard in selectedCards)
            {
                await CardCmd.AutoPlay(choiceContext, selectedCard, null);
            }
        }
    }

    public async Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var handCards = PileType.Hand.GetPile(Owner).Cards.ToList();
        if (handCards.Count > 0)
        {
            var selectedCards = await CardSelectCmd.FromCombatPile(choiceContext,
                PileType.Hand.GetPile(Owner),
                Owner,
                CardSelectorPrompt.ToPlay.GetFixedPrefs(DynamicVars.Cards.IntValue)
            );

            foreach (var selectedCard in selectedCards)
            {
                selectedCard.BaseReplayCount += DynamicVars.Repeat.IntValue - 1;
                await CardCmd.AutoPlay(choiceContext, selectedCard, null);
                selectedCard.BaseReplayCount -= DynamicVars.Repeat.IntValue - 1;
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(1);
    }
}