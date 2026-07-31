using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Cards.Token;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class BandPractice() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Common;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromCard<MelodyFragments>(IsUpgraded)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var selectedCards = await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            CardSelectorPrompt.ToTransform.GetFixedPrefs(1),
            card => card != this,
            this
        );

        foreach (var selectedCard in selectedCards)
        {
            NCombatRoom.Instance?.Ui.Hand.Remove(selectedCard);

            await CardPileCmd.Add(selectedCard, BangDreamConst.ExtraDraw, CardPilePosition.Top, skipVisuals: true);

            var transformResult = await CardCmd.TransformTo<MelodyFragments>(selectedCard);

            if (transformResult is { success: true })
            {
                var transformedCard = transformResult.Value.cardAdded;
                if (IsUpgraded) CardCmd.Upgrade(transformedCard);
            }
        }
    }
}