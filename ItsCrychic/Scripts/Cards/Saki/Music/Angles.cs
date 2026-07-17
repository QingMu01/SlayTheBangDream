using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class Angles() : AbstractSakikoMusicCard(CardRarity.Rare, TargetType.None)
{
    public override bool IsInstant => true;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CardVars => [];

    public override async Task OnPerform(PlayerChoiceContext choiceContext)
    {
        ArgumentNullException.ThrowIfNull(CombatState);
        var manager = Owner.AttachedData().PerformManager;
        var candidates = CardFactory.FilterForCombat(ModelDb.AllCards.Where(card =>
            card is { Type: CardType.Attack, Rarity: CardRarity.Rare })).ToList();

        while (manager.PerformPile.Cards.Count < manager.Capacity && candidates.Count > 0)
        {
            var prototype = Owner.RunState.Rng.CombatCardGeneration.NextItem(candidates);
            if (prototype == null) break;
            var card = CombatState.CreateCard(prototype, Owner);
            if (IsUpgraded) CardCmd.Upgrade(card);
            card.EnergyCost.SetThisTurnOrUntilPlayed(0, true);
            await CardPileCmd.AddGeneratedCardToCombat(card, BangDreamConst.PerformPile, Owner);
            await Cmd.CustomScaledWait(0.1f, 0.2f);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级生成升级后的攻击牌。
    }
}
