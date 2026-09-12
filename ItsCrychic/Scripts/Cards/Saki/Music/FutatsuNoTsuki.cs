using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils.Infos;
using ItsCrychic.Scripts.Character;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class FutatsuNoTsuki() : AbstractSakikoMusicCard(CardRarity.Rare, TargetType.None)
{
    public override bool IsInstant => true;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Cards.Create(1)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var mutsumi = ModelDb.Character<WakabaMutsumi>();
        if (!mutsumi.AllowSelect) return;

        var allCards = CardFactory.FilterForCombat(mutsumi.CardPool.AllCards).ToList();
        var candidates = new List<CardModel>();
        foreach (var rarity in new[] { CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare })
        {
            var prototype = Owner.RunState.Rng.CombatCardGeneration.NextItem(
                allCards.Where(card => card.Rarity == rarity));
            if (prototype == null) return;
            candidates.Add(prototype);
        }

        var previews = candidates.Select(c => CombatState.CreateCard(c, Owner)).ToList();
        if (IsUpgraded)
        {
            foreach (var preview in previews.Where(card => card.IsUpgradable)) CardCmd.Upgrade(preview);
        }

        var selected = await CardSelectCmd.FromChooseACardScreen(choiceContext, previews, Owner, true);
        if (selected == null || previews.IndexOf(selected) < 0) return;

        var generatedCard = CombatState.CreateCard(selected, Owner);
        if (IsUpgraded && generatedCard.IsUpgradable) CardCmd.Upgrade(generatedCard);

        await CardPileCmd.AddGeneratedCardToCombat(generatedCard, PileType.Hand, Owner);
    }
}