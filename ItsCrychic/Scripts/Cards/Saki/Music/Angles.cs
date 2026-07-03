using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class Angles() : AbstractSakikoMusicCard(CardRarity.Rare, TargetType.None)
{
    public override bool IsInstant { get; set; } = true;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust,
        BangDreamConst.Perform,
        BangDreamConst.Instant
    ];

    protected override IEnumerable<DynamicVar> CardVars => [];

    public override async Task OnStopPerformance(PlayerChoiceContext choiceContext)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var manager = Owner.AttachedData().PerformanceManager;
        var candidates = CardFactory.FilterForCombat(Owner.Character.CardPool.AllCards)
            .Where(card => card is { Type: CardType.Attack, Rarity: CardRarity.Rare })
            .ToList();

        while (manager.PerformancePile.Cards.Count < manager.Capacity && candidates.Count > 0)
        {
            var template = Owner.RunState.Rng.CombatCardGeneration.NextItem(candidates);
            if (template == null) break;

            var generatedCard = CombatState.CreateCard(template, Owner);
            if (IsUpgraded)
            {
                CardCmd.Upgrade(generatedCard);
            }

            generatedCard.EnergyCost.SetUntilPlayed(0);

            await CardPileCmd.AddGeneratedCardToCombat(generatedCard, BangDreamConst.PerformPile, Owner);
        }
    }
}