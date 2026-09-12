using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class DivineCarriage() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.AllEnemies;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        ModCardVars.Int("LingeredPenalty", 5),
        ComputedDynamicVarHelper.CreateDamageVar("CalcDamage", 25m, ctx =>
        {
            if (ctx.IsInCombat() && ctx.ActiveCard.DynamicVars.TryGetValue("LingeredPenalty", out var penalty))
            {
                var lingered = SecondaryResourceCmd.Get(ctx.ActiveCard.Owner, BangDreamConst.LingeredResource);
                return Math.Max(0, ctx.BaseValue - lingered * penalty.BaseValue);
            }

            return ctx.BaseValue;
        })
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        await DamageCmd.Attack(DynamicVars.ComputedValue("CalcDamage"))
            .FromCard(this, play)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["CalcDamage"].UpgradeValueBy(5m);
    }
}