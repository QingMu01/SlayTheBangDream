using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Keywords;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class Pointillism() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.Self;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust,
        BangDreamConst.KeywordPerformanceArea.GetModCardKeyword()
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        ModCardVars.Computed("Size", 1,
            (card, _) => card == null ? 0 : BangDreamConst.PilePerformance.GetPile(card.Owner).Cards.Count,
            (card, _, _, _) => card == null ? 0 : BangDreamConst.PilePerformance.GetPile(card.Owner).Cards.Count)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var capacity = Owner.Character is IPerformanceCharacter performanceCharacter
            ? performanceCharacter.GetDefaultCapacity
            : 0;

        if (capacity > 0)
        {
            await ExtraPileCmd.Draw(choiceContext, capacity, Owner);
        }

        var performance = BangDreamConst.PilePerformance.GetPile(Owner);
        var cardsToDiscard = performance.Cards.ToList();

        foreach (var card in cardsToDiscard)
        {
            await CardPileCmd.Add(card, PileType.Discard);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}