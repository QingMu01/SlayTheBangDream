using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Characters;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class Pulverise() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), ISubsideCard
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    private int _resolvedEnergyX;

    public int LingeredResourceCost => 4;

    protected override bool HasEnergyCostX => true;

    protected override IEnumerable<CardKeyword> CardKeywords => [BangDreamConst.Lingered];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(5),
        QuickVar.Energy.Create(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        _resolvedEnergyX = ResolveEnergyXValue();
        if (_resolvedEnergyX <= 0) return;

        var attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, play)
            .Targeting(play.Target)
            .WithHitCount(_resolvedEnergyX)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        if (attack.Results.SelectMany(results => results).Any(result => result.WasTargetKilled))
        {
            await PlayerCmd.GainEnergy(_resolvedEnergyX + (IsUpgraded ? 1 : 0), Owner);
            await CardPileCmd.Add(this, PileType.Hand);
        }
    }

    public Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        return _resolvedEnergyX > 0
            ? PlayerCmd.GainEnergy(_resolvedEnergyX, Owner)
            : Task.CompletedTask;
    }

    public override Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card == this) _resolvedEnergyX = 0;
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        // 升级后休止所需余音减少 1 点。
        this.SecondaryCosts().Set(BangDreamConst.LingeredResource, 3);
    }
}
