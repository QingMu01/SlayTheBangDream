using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class Pointillism() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.Self;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust,
        BangDreamConst.PerformArea
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var selectedCards = await CardSelectCmd.FromHand(choiceContext, Owner,
            CardSelectorPrompt.ToPerformance.GetUnlimitedPrefs(), card => card != this, this);
        var manager = Owner.AttachedData().PerformManager;
        foreach (var selectedCard in selectedCards)
        {
            var displacedCard = manager.GetCardDisplacedBy(selectedCard);
            if (displacedCard is IPerformCard)
            {
                await manager.PerformCard(displacedCard);
            }

            await CardPileCmd.Add(selectedCard, BangDreamConst.PerformPile);
            await Cmd.CustomScaledWait(0.1f, 0.2f);
        }
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
