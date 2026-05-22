using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class Blessing() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), ISubsideCardFlag
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Common;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    public override bool GainsBlock => true;

    public int LingeredEnergyCost => 1;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        ModCardVars.Computed("BaseDamage", 4m,
            card => DynamicVarHelper.ResolveBaseVar(card, CalcIncrease),
            (card, mode, target, runHooks) =>
                DynamicVarHelper.ResolvePreviewDamageVar(card, mode, target, runHooks, CalcIncrease)),
        ModCardVars.Computed("BaseBlock", 4m,
            card => DynamicVarHelper.ResolveBaseVar(card, CalcIncrease),
            (card, mode, target, runHooks) =>
                DynamicVarHelper.ResolvePreviewBlockVar(card, mode, target, runHooks, CalcIncrease)),
        QuickVar.Repeat.Create(0),
        new IntVar("IncreaseStep", 1)
    ];


    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        await DamageCmd.Attack(this.DynamicVar("BaseDamage").BaseValue)
            .FromCard(this)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        await CreatureCmd.GainBlock(Owner.Creature,
            new BlockVar(this.DynamicVar("BaseBlock").BaseValue, ValueProp.Move), play);
    }

    public Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        QuickVar.Repeat.Get(DynamicVars).BaseValue += DynamicVars["IncreaseStep"].BaseValue;
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars["IncreaseStep"].UpgradeValueBy(1m);
    }

    private static decimal CalcIncrease(CardModel? card)
    {
        if (card != null && card.DynamicVars.TryGetValue("Repeat",out var dynamicVar))
        {
            return 4m + dynamicVar.BaseValue;
        }

        return 4m;
    }
}