using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class Pulverise() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), ISubsideCard
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    public int LingeredResourceCost => 5;

    protected override bool HasEnergyCostX => true;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(8),
        QuickVar.Energy.Create(1),
    ];


    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var energyToCost = ResolveEnergyXValue();
        if (energyToCost > 0)
        {
            var energyToGain = energyToCost + (IsUpgraded ? 1 : 0);

            var attackCommand = await DamageCmd.Attack(DynamicVars.Damage.BaseValue * ResolveEnergyXValue())
                .FromCard(this, play)
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
