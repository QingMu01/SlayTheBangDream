using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using ItsCrychic.Scripts.Power.Debuff;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class ChoirSChoir() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None)
{
    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromPower<ChoirFallenPower>(),
        HoverTipFactory.FromPower<ChoirLockPower>()
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(9),
        QuickVar.Buff.Create(6)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
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
            if (IsUpgraded)
            {
                await PowerCmd.Apply<ChoirLockPower>(choiceContext, target,
                    QuickVar.Buff.GetVar(this).BaseValue, Owner.Creature, this);
            }
            else
            {
                await PowerCmd.Apply<ChoirFallenPower>(choiceContext, target,
                    QuickVar.Buff.GetVar(this).BaseValue, Owner.Creature, this);
            }
        }
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner && Pile?.Type == BangDreamConst.PerformPile)
        {
            await Owner.AttachedData().PerformManager.PerformCard(this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
        QuickVar.Buff.GetVar(this).UpgradeValueBy(2);
    }
}