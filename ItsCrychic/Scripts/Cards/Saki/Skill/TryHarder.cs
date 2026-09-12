using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Cards.Token;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class TryHarder() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<CardKeyword> CardKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromCard<BasicScale>(IsUpgraded)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);
        ArgumentNullException.ThrowIfNull(Owner.PlayerCombatState);

        var manager = Owner.AttachedData().PerformManager;
        manager.AddCapacity(Math.Max(0, 7 - manager.Capacity));

        var musicCards = Owner.PlayerCombatState.AllCards
            .Where(card => card.Pile?.Type != PileType.Exhaust)
            .Concat(manager.PerformPile.Cards)
            .Concat(BangDreamConst.ExtraDraw.GetPile(Owner).Cards)
            .OfType<IPerformCard>()
            .Cast<CardModel>()
            .Distinct()
            .ToList();

        var needAdd = new List<CardModel>();
        foreach (var musicCard in musicCards)
        {
            await CardPileCmd.Add(musicCard, PileType.Exhaust);
            var scale = CombatState.CreateCard<BasicScale>(Owner);
            if (IsUpgraded) CardCmd.Upgrade(scale);
            needAdd.Add(scale);
        }

        await CardPileCmd.AddGeneratedCardsToCombat(needAdd, BangDreamConst.PerformPile, Owner);
    }
}