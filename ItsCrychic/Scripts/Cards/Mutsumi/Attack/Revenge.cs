using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Mutsumi.Attack;

public class Revenge() : AbstractMutsumiCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(4),
        ComputedDynamicVarHelper.CreateBaseVar("RepeatCount", 2m, ctx =>
        {
            if (ctx.IsInCombat() && ctx.Target != null)
            {
                var extraHits = CombatManager.Instance.History.Entries
                    .OfType<CreatureAttackedEntry>()
                    .Where(entry => entry.Actor == ctx.Target)
                    .SelectMany(entry => entry.DamageResults)
                    .Count(result => result.Receiver == ctx.ActiveCard.Owner.Creature && result.UnblockedDamage > 0);
                return ctx.BaseValue + extraHits;
            }

            return ctx.BaseValue;
        })
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, play)
            .WithHitCount((int)DynamicVars.ComputedValue("RepeatCount", play.Target))
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1);
    }
}