using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Combat.AttackHits;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class Legato() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), IAttackHitHookListener
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Common;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.PerformArea
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(4),
        ComputedDynamicVarHelper.CreateBaseVar("Count", 0, ctx =>
        {
            if (ctx.IsInCombat())
            {
                return BangDreamConst.PerformPile.GetPile(ctx.ActiveCard.Owner).Cards.Count;
            }

            return ctx.BaseValue;
        })
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        var hitCount = DynamicVars.ComputedValue("Count");
        if (hitCount <= 0)
        {
            return;
        }

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, play)
            .Targeting(play.Target)
            .WithHitCount((int)hitCount)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}