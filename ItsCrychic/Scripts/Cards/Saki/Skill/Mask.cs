using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class Mask() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Common;
    private const TargetType CustomTarget = TargetType.Self;

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new CalculationBaseVar(0m),
        new CalculationExtraVar(1m),
        new CalculatedBlockVar(ValueProp.Move).WithMultiplier((card, _) => CalculateExtraBlock(card)),
        new IntVar("MusicCardMultiple", 3),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (DynamicVars.Block.BaseValue > 0)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.CalculatedBlock.Calculate(play.Target),
                DynamicVars.CalculatedBlock.Props, play);
        }
    }

    private static decimal CalculateExtraBlock(CardModel card)
    {
        var costCount = 0m;
        foreach (var cardModel in BangDreamConst.PileExtraDraw.GetPile(card.Owner).Cards)
        {
            costCount += cardModel.EnergyCost.GetResolved();
            if (card.IsUpgraded && cardModel is IPerformanceCard)
            {
                costCount += card.DynamicVars["MusicCardMultiple"].IntValue;
            }
        }

        return costCount;
    }
}