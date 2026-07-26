using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class Kanon() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Innate,
        CardKeyword.Exhaust,
        BangDreamConst.PerformArea,
        BangDreamConst.Music
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var manager = Owner.AttachedData().PerformManager;

        var emptySlots = Math.Max(0, manager.Capacity - manager.PerformPile.Cards.Count);
        if (emptySlots == 0) return;

        var musicCards = CardFactory.FilterForCombat(BangDreamTools.GetCharacterExtraCards(Owner, true)).ToList();
        if (musicCards.Count == 0) return;

        var selectedCards = Enumerable.Range(0, emptySlots)
            .Select(_ => Owner.RunState.Rng.CombatCardGeneration.NextItem(musicCards))
            .Where(card => card != null)
            .Select(card => CombatState.CreateCard(card!, Owner))
            .ToList();

        CardCmd.PreviewCardPileAdd(
            await CardPileCmd.AddGeneratedCardsToCombat(selectedCards, BangDreamConst.PerformPile, Owner)
        );
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
