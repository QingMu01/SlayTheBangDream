using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Keywords;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class Evacuation() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Common;
    private const TargetType CustomTarget = TargetType.Self;

    public override bool GainsBlock => true;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.KeywordPerformanceArea.GetModCardKeyword()
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new BlockVar(4m, ValueProp.Move),
        new BlockVar("ExtraBlock", 5m, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        var performance = BangDreamConst.PilePerformance.GetPile(Owner);
        for (var i = performance.Cards.Count; i > 0; i--)
        {
            await CreatureCmd.GainBlock(Owner.Creature, (BlockVar)DynamicVars["ExtraBlock"], play, true);
            var card = performance.Cards[i];
            if (card is IPerformanceCard performanceCard)
            {
                await CardPileCmd.Add(card, performanceCard.WhenStopMoveToPile);
            }
            else
            {
                await CardPileCmd.Add(card, PileType.Discard);
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(1m);
        DynamicVars["ExtraBlock"].UpgradeValueBy(1m);
    }
}