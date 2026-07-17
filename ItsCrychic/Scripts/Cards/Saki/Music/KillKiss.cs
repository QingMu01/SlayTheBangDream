using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class KillKiss() : AbstractSakikoMusicCard(CardRarity.Rare, TargetType.RandomEnemy)
{
    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new BoolVar("IsInHand", false),
        ComputedDynamicVarHelper.CreateDamageVar("CalcDamage", 15m, ctx =>
        {
            if (ctx.IsInCombat() && ctx.ActiveCard.DynamicVars.TryGetValue("IsInHand", out var isInHand))
            {
                if (isInHand is BoolVar boolVar)
                {
                    return boolVar.BoolVal ? ctx.BaseValue * 2m : ctx.BaseValue;
                }
            }

            return ctx.BaseValue;
        })
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var target = Owner.RunState.Rng.CombatTargets.NextItem(CombatState.HittableEnemies);
        if (target != null)
        {
            await DamageCmd.Attack(DynamicVars.ComputedValue("CalcDamage"))
                .FromCard(this, null)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }
    }

    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (card == this && card.Pile?.Type == PileType.Hand)
        {
            if (card.DynamicVars.TryGetValue("IsInHand", out var isInHand) && isInHand is BoolVar boolVar)
            {
                boolVar.BoolVal = true;
            }
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars["CalcDamage"].UpgradeValueBy(5);
    }
}