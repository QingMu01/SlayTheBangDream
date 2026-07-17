using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class Moonlight() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.MusicNote
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        ModCardVars.Int("TrackTurn", 2),
        ComputedDynamicVarHelper.CreateDamageVar("CalcDamage", 7m, CalculateDamage)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        await DamageCmd.Attack(DynamicVars.ComputedValue("CalcDamage"))
            .FromCard(this, play)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["TrackTurn"].UpgradeValueBy(1);
    }

    private static decimal CalculateDamage(BangDreamComputedVar.ComputedVarsContext ctx)
    {
        if (ctx.IsInCombat() && ctx.ActiveCard.DynamicVars.TryGetValue("TrackTurn", out var dynamicVar))
        {
            var allDamage = ctx.BaseValue;
            var tracker = ctx.ActiveCard.Owner.AttachedData().MusicNoteDamageTracker;
            if (tracker.CombatHistory.Count == 0)
            {
                return allDamage;
            }

            var tracebackTurn = Math.Min(dynamicVar.IntValue, tracker.CombatHistory.Count);
            for (var i = tracebackTurn - 1; i >= 0; i--)
            {
                allDamage += tracker.GetTurnDamageResults(i + 1).Sum(x => x.TotalDamage);
            }
        }

        return ctx.BaseValue;
    }
}