using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Features.Rule;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class Pulverise() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), ISubsideCard
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    public int LingeredResourceCost => 3;

    protected override bool HasEnergyCostX => true;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Energy.Create(1),
        ModCardVars.Int("FixedDamage", 4),
        ComputedDynamicVarHelper.CreateDamageVar("CalcDamage", 8m, ctx =>
        {
            if (ctx.IsInCombat() && ctx.ActiveCard.DynamicVars.TryGetValue("FixedDamage", out var fixedDamage))
            {
                if (LingeredResourcesRule.IsSufficient(ctx.ActiveCard))
                {
                    return fixedDamage.IntValue + ctx.BaseValue;
                }
            }

            return ctx.BaseValue;
        })
    ];


    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var energyToCost = ResolveEnergyXValue();
        if (energyToCost > 0)
        {
            var energyToGain = energyToCost + (IsUpgraded ? 1 : 0);

            var attackCommand = await DamageCmd.Attack(DynamicVars.ComputedValue("CalcDamage"))
                .FromCard(this, play)
                .Targeting(play.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

            if (energyToGain > 0)
            {
                // 检查是否斩杀
                if (attackCommand.Results.SelectMany(r => r).Any(result => result.WasTargetKilled))
                {
                    await PlayerCmd.GainEnergy(energyToGain, Owner);
                    await CardPileCmd.Add(this, PileType.Hand);
                }
            }
        }
    }

    public Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        return Task.CompletedTask;
    }
}