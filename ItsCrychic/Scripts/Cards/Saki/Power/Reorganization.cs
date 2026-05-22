using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Power.Buff;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Keywords;

namespace ItsCrychic.Scripts.Cards.Saki.Power;

public class Reorganization() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Power;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.Self;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.KeywordPerformanceArea.GetModCardKeyword(),
        BangDreamConst.KeywordLinger.GetModCardKeyword()
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new IntVar("Reorganization", 1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PowerCmd.Apply<ReorganizationPower>(choiceContext, Owner.Creature, DynamicVars["Reorganization"].IntValue,
            Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Reorganization"].UpgradeValueBy(1);
    }
}