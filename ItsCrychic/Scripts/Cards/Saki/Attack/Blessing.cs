using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class Blessing() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), ISubsideCard
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Common;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    public override bool GainsBlock => true;

    public int LingeredResourceCost => 2;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        ComputedDynamicVarHelper.CreateDamageVar("CalcDamage", 5m, CalcIncrease),
        ComputedDynamicVarHelper.CreateBlockVar("CalcBlock", 5m, CalcIncrease),
        ModCardVars.Int("IncreaseStep", 2),
        QuickVar.Repeat.Create(0)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        await DamageCmd.Attack(DynamicVars.ComputedValue("CalcDamage", play.Target))
            .FromCard(this, play)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        await CreatureCmd.GainBlock(Owner.Creature,
            new BlockVar(DynamicVars.ComputedValue("CalcBlock"), ValueProp.Move), play);
    }

    public Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        DynamicVars.Repeat.BaseValue++;
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars["IncreaseStep"].UpgradeValueBy(1m);
    }

    private static decimal CalcIncrease(BangDreamComputedVar.ComputedVarsContext ctx)
    {
        if (ctx.IsInCombat() && ctx.ActiveCard.DynamicVars.TryGetValue("IncreaseStep", out var dynamicVar))
        {
            return ctx.BaseValue + dynamicVar.BaseValue * ctx.ActiveCard.DynamicVars.Repeat.BaseValue;
        }

        return ctx.BaseValue;
    }
}