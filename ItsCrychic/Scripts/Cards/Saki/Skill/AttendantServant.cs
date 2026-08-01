using ItsCrychic.Scripts.Cards.Token;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

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
        HoverTipFactory.FromCard<SakikoShield>(IsUpgraded)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var selectedCard = Owner.RunState.Rng.CombatCardSelection.NextItem(Owner.PlayerCombatState?.Hand.Cards ?? []);
        if (selectedCard != null)
        {
            var transformResult = await CardCmd.TransformTo<SakikoShield>(selectedCard);
            if (IsUpgraded && transformResult is { success: true })
            {
                CardCmd.Upgrade(transformResult.Value.cardAdded);
            }

            if (selectedCard.DeckVersion == null)
            {
                await CardPileCmd.Add(this, PileType.Hand);
            }
        }
    }
}