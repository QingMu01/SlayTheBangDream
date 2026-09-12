using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.Characters;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class Pulverise() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), ISubsideCard
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    private int _resolvedEnergyX;

    public int LingeredResourceCost => 5;

    protected override bool HasEnergyCostX => true;

    protected override IEnumerable<CardKeyword> CardKeywords => [BangDreamConst.Lingered];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(10),
        QuickVar.Energy.Create(1),
        ComputedDynamicVarHelper.CreateDamageVar("CalcDamage", 0, ctx =>
        {
            if (ctx.IsInCombat() && ctx.ActiveCard.DynamicVars.TryGetValue(DamageVar.defaultName, out var damageVar))
            {
                Hook.ModifyDamage(ctx.ActiveRunState, ctx.ActiveCombatState,
                    ctx.Target, ctx.ActiveCard.Owner.Creature,
                    damageVar.BaseValue, ValueProp.Move, ctx.ActiveCard,
                    null,
                    ModifyDamageHookType.All, CardPreviewMode.Normal,
                    out _);
                return damageVar.BaseValue * ctx.ActiveCard.Owner.GetEnergy();
            }

            return ctx.BaseValue;
        })
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        _resolvedEnergyX = ResolveEnergyXValue();
        if (_resolvedEnergyX <= 0) return;

        var attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue * _resolvedEnergyX)
            .FromCard(this, play)
            .Targeting(play.Target)
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
}