using ItsCrychic.Scripts.Cards.Token;
using ItsCrychic.Scripts.Character.CardPools;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class AttendantServant() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.Self;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Retain
    ];

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromCard<SakikoShield>()
    ];

    private bool _returnToHand;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        _returnToHand = false;
        var selectedCard = Owner.RunState.Rng.CombatCardSelection.NextItem(Owner.PlayerCombatState?.Hand.Cards ?? []);
        if (selectedCard == null) return;

        var belongsToSakiko = selectedCard.Pool != ModelDb.CardPool<SakikoStandardCardPool>() ||
                              selectedCard.Pool != ModelDb.CardPool<SakikoMusicalCardPool>();

        _returnToHand = !belongsToSakiko;

        var transformResult = await CardCmd.TransformTo<SakikoShield>(selectedCard);
        if (IsUpgraded && transformResult is { success: true })
        {
            CardCmd.Upgrade(transformResult.Value.cardAdded);
        }
    }

    protected override CardLocation GetResultLocationForCardPlay()
    {
        return _returnToHand
            ? new CardLocation(Owner, PileType.Hand, CardPilePosition.Bottom)
            : base.GetResultLocationForCardPlay();
    }
}