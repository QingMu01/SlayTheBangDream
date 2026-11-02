using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class RehearsalPrep() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Common;
    private const TargetType CustomTarget = TargetType.Self;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Repeat.Create(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var performPile = BangDreamConst.PerformPile.GetPile(Owner);
        if (performPile.Cards.Count == 0) return;

        var selectedCard = (await CardSelectCmd.FromCombatPile(choiceContext,
            performPile,
            Owner,
            CardSelectorPrompt.ToExtraDraw.GetFixedPrefs(1))).FirstOrDefault();
        if (selectedCard == null) return;

        var manager = Owner.AttachedData().PerformManager;
        for (var i = 0; i < DynamicVars.Repeat.IntValue; i++)
        {
            await manager.PerformCard(selectedCard);
        }

        await CardPileCmd.Add(selectedCard, BangDreamConst.ExtraDraw, CardPilePosition.Random);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(1);
    }
}
