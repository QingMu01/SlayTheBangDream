using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Cards.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class Moonlight() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new IntVar("TrackTurn", 2),
        ModCardVars.Computed("CalcDamage", 7m, card =>
                DynamicVarHelper.ResolveBaseVar(card, CalculateDamage),
            (card, mode, target, runHooks) =>
                DynamicVarHelper.ResolvePreviewDamageVar(card, mode, target, runHooks, CalculateDamage))
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        await DamageCmd.Attack(this.DynamicVar<ComputedDynamicVar>("CalcDamage").Calculate())
            .FromCard(this)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["TrackTurn"].UpgradeValueBy(1);
    }

    private static decimal CalculateDamage(CardModel? card)
    {
        var allDamage = 7m;
        if (card != null)
        {
            var intVar = card.DynamicVar<IntVar>("TrackTurn");
            var tracker = card.Owner.AttachedData().MusicNoteDamageTracker;
            var tracebackTurn = Math.Min(intVar.IntValue, tracker.HistoryLength);
            for (var i = tracebackTurn - 1; i >= 0; i--)
            {
                allDamage += tracker.GetTurnDamagedTotal(i);
            }
        }

        return allDamage;
    }
}