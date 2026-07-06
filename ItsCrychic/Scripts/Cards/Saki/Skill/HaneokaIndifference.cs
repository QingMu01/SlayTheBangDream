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

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Ethereal
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Block.Create(6),
        QuickVar.Cards.Create(2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);

        var selectedCards = await CardSelectCmd.FromCombatPile(choiceContext,
            PileType.Draw.GetPile(Owner),
            Owner,
            CardSelectorPrompt.ToExtraDraw.GetLimitedPrefs(DynamicVars.Cards.IntValue)
        );

        foreach (var selectedCard in selectedCards)
        {
            await CardPileCmd.Add(selectedCard, BangDreamConst.ExtraDraw);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}