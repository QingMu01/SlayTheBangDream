using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class AnglesSakiko() : AbstractSakikoMusicCard(CustomRarity, CustomTarget)
{
    private const CardRarity CustomRarity = CardRarity.Rare;
    private static readonly TargetType CustomTarget = PerformTargetTypes.RequestGroup;

    // 取存在的最高稀有度，档内随机。
    private static readonly CardRarity[] RarityPriority =
    [
        CardRarity.Rare,
        CardRarity.Uncommon,
        CardRarity.Common,
        CardRarity.Basic
    ];

    public override bool IsInstant => true;

    protected override IEnumerable<DynamicVar> CardVars => [];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var deckCards = PileType.Deck.GetPile(Owner).Cards
            .Where(card => card.Type == CardType.Attack)
            .ToList();
        if (deckCards.Count == 0) return;

        var candidates = RarityPriority
            .Select(rarity => deckCards.Where(card => card.Rarity == rarity).ToList())
            .FirstOrDefault(cards => cards.Count > 0);
        // 未列入优先级的稀有度参与兜底随机。
        candidates ??= deckCards;

        var selectedCard = Owner.RunState.Rng.CombatCardSelection.NextItem(candidates);
        if (selectedCard == null) return;

        var combatCard = CombatState.CloneCard(selectedCard);
        if (IsUpgraded)
        {
            CardCmd.Upgrade(combatCard);
        }

        await CardCmd.AutoPlay(choiceContext, combatCard, null);
    }
}
