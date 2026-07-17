using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class FinishTimes() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 2;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.PerformArea
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        ComputedDynamicVarHelper.CreateDamageVar("CalcDamage", 7m, ctx =>
        {
            if (ctx.IsInCombat())
            {
                var performanceCards = BangDreamConst.PerformPile.GetPile(ctx.ActiveCard.Owner).Cards.ToList();
                if (performanceCards.GroupBy(card => card.Type).Any(group => group.Count() >= 3))
                {
                    var multiplier = ctx.ActiveCard.IsUpgraded ? 2m : 1.5m;
                    return Math.Ceiling(ctx.BaseValue * multiplier);
                }
            }

            return ctx.BaseValue;
        }),
        QuickVar.Repeat.Create(3)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        await DamageCmd.Attack(DynamicVars.ComputedValue("CalcDamage"))
            .FromCard(this, play)
            .WithHitCount(DynamicVars.Repeat.IntValue)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }
}