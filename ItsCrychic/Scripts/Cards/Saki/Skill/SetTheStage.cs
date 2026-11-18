using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.CardTargeting;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class SetTheStage() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private static readonly TargetType CustomTarget = CustomTargetType.Anyone;

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new BlockVar(11m, ValueProp.Move | ValueProp.Unpowered)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var playTarget = play.Target;
        if (playTarget != null)
        {
            await CreatureCmd.LoseBlock(choiceContext, playTarget, playTarget.Block, Owner.Creature);
            // 本卡把格挡给「目标」（可能是敌人），不是拥有者获得格挡，因此**刻意不覆写** GainsBlock。
            // 已核对本体消费点：Nimble.CanEnchant 要求「卡牌本身给拥有者格挡」；
            // StretchPower.BeforeCardPlayed/ModifyBlockAdditive 会把增益加给该卡下一次格挡（不区分目标），
            // 若此处声明 GainsBlock=true 反而会让敌方格挡吃到我方力量类加成。
            await CreatureCmd.GainBlock(playTarget, DynamicVars.Block, play);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(4m);
    }
}
