using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Features.Rule;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class BitterChoice()
    : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), ISubsideCard
{
    private const int CustomCost = 2;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Common;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    public int LingeredResourceCost => 2;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.Lingered
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(20),
        QuickVar.Cards.Create(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, play)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        if (!LingeredResourcesRule.IsSufficient(this))
        {
            var discardPileCards = PileType.Discard.GetPile(Owner).Cards.ToList();
            for (var i = 0; i < DynamicVars.Cards.IntValue; i++)
            {
                var cardModel = Owner.RunState.Rng.CombatCardSelection.NextItem(discardPileCards);
                if (cardModel != null)
                {
                    await CardPileCmd.Add(cardModel, BangDreamConst.ExtraDraw);
                }
                else
                {
                    return;
                }
            }
        }
    }

    public async Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var selectedCards = await CardSelectCmd.FromCombatPile(choiceContext,
            PileType.Discard.GetPile(Owner),
            Owner,
            CardSelectorPrompt.ToExtraDraw.GetLimitedPrefs(DynamicVars.Cards.IntValue, true, true)
        );
        foreach (var selectedCard in selectedCards)
        {
            await CardPileCmd.Add(selectedCard, BangDreamConst.ExtraDraw);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1);
    }
}