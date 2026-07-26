using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class KillKiss() : AbstractSakikoMusicCard(CardRarity.Rare, TargetType.RandomEnemy), IPerformHookListener
{
    protected override bool ShouldGlowGoldInternal
    {
        get
        {
            if (IsMutable && DynamicVars.TryGetValue("IsInHand", out var isInHand) && isInHand is BoolVar boolVar)
            {
                return boolVar.BoolVal;
            }

            return false;
        }
    }

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

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!cardPlay.IsAutoPlay)
        {
            if (DynamicVars["IsInHand"] is BoolVar var) var.BoolVal = true;
        }

        return Task.CompletedTask;
    }

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

        if (DynamicVars["IsInHand"] is BoolVar var) var.BoolVal = false;
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

    public async Task OnCardEnterPerformArea(PlayerChoiceContext choiceContext, CardModel cardModel)
    {
        if (cardModel == this && DynamicVars["IsInHand"] is BoolVar { BoolVal: true })
        {
            await Owner.AttachedData().PerformManager.PerformCard(this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["CalcDamage"].UpgradeValueBy(5);
    }
}