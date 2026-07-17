using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class PathDependence() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), ISubsideCard
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.None;

    public int LingeredResourceCost => -1;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Cards.Create(0),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var selectedCards = await CardSelectCmd.FromHand(choiceContext, Owner,
            CardSelectorPrompt.ToExtraDraw.GetUnlimitedPrefs(), _ => true, this);
        DynamicVars.Cards.BaseValue = 0;
        foreach (var selectedCard in selectedCards)
        {
            DynamicVars.Cards.BaseValue++;
            await CardPileCmd.Add(selectedCard, BangDreamConst.ExtraDraw);
        }
    }

    public async Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var needDrawCount = DynamicVars.Cards.BaseValue + (IsUpgraded ? 1 : 0);
        if (needDrawCount > 0)
        {
            await ExtraPileCmd.Draw(choiceContext, needDrawCount, Owner);
        }
    }
}
