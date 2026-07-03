using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Cards.Token;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class InspirationBurst() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.Self;

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromCard<MelodyFragments>(IsUpgraded)
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Cards.Create(2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);
        var performanceCards = BangDreamTools.GetPile(BangDreamConst.PerformPile, Owner).Cards.ToList();
        foreach (var cardModel in performanceCards)
        {
            if (cardModel is MelodyFragments otherCard && cardModel != this)
            {
                await otherCard.OnStartPerformance(choiceContext);
            }
        }

        for (var i = 0; i < DynamicVars.Cards.IntValue; i++)
        {
            var melodyCard = CombatState.CreateCard<MelodyFragments>(Owner);
            if (IsUpgraded) CardCmd.Upgrade(melodyCard);
            await CardPileCmd.AddGeneratedCardToCombat(melodyCard, PileType.Hand, Owner);
        }
    }
}