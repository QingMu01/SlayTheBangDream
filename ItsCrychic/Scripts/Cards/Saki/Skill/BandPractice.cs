using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Cards.Token;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

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
        var selectedCards = await CardSelectCmd.FromHand(choiceContext, Owner,
            CardSelectorPrompt.ToTransform.GetFixedPrefs(1), card => card != this, this);
        foreach (var selectedCard in selectedCards)
        {
            await CardPileCmd.Add(selectedCard, PileType.Play);
            var cardPileAddResult = await CardCmd.TransformTo<MelodyFragments>(selectedCard);
            if (cardPileAddResult is { success: true })
            {
                if (IsUpgraded)
                {
                    CardCmd.Upgrade(cardPileAddResult.Value.cardAdded);
                }

                await Cmd.CustomScaledWait(0.15f, 0.3f);
                await CardPileCmd.Add(cardPileAddResult.Value.cardAdded, BangDreamConst.ExtraDraw);
            }
        }
    }
}