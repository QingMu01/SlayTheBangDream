using BangDreamLib.Scripts.Interfaces.CardAugment;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class Pulverise() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), ISubsideCardFlag
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    public int LingeredEnergyCost => 5;

    protected override bool HasEnergyCostX => true;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new DamageVar(8m, ValueProp.Move),
        new EnergyVar(1)
    ];


    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var energyToCost = ResolveEnergyXValue();
        if (energyToCost > 0)
        {
            var energyToGain = IsUpgraded ? energyToCost : energyToCost - 1;

            var attackCommand = await DamageCmd.Attack(DynamicVars.Damage.BaseValue * ResolveEnergyXValue())
                .FromCard(this)
                .Targeting(play.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

            // 检查是否斩杀
            if (energyToGain > 0)
            {
                if (attackCommand.Results.SelectMany(r => r).Any(result => result.WasTargetKilled))
                {
                    await PlayerCmd.GainEnergy(energyToGain, Owner);
                }
            }
        }
    }

    public async Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CardPileCmd.Add(this, PileType.Hand);
    }
}