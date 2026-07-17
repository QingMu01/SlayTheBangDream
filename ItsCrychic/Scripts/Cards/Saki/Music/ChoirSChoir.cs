using BangDreamLib.Scripts.Extensions;
using ItsCrychic.Scripts.Power.Debuff;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class ChoirSChoir() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None)
{
    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromPower<ChoirLockPower>()
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(8),
        QuickVar.Buff.Create(5)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext)
    {
        ArgumentNullException.ThrowIfNull(CombatState);
        var candidates = CombatState.HittableEnemies.ToList();
        var locked = candidates.Where(enemy => enemy.GetPower<ChoirLockPower>() != null).ToList();
        var target = Owner.RunState.Rng.CombatTargets.NextItem(locked.Count > 0 ? locked : candidates);
        if (target == null) return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, null)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        if (target.IsHittable)
        {
            await PowerCmd.Apply<ChoirLockPower>(choiceContext, target, QuickVar.Buff.GetVar(this).BaseValue,
                Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        QuickVar.Buff.GetVar(this).UpgradeValueBy(3);
    }
}