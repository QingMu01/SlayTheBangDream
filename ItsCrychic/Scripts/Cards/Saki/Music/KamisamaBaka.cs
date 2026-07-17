using BangDreamLib.Scripts.Extensions;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class KamisamaBaka() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None)
{
    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    protected override IEnumerable<DynamicVar> CardVars => [QuickVar.Damage.Create(10)];

    public override async Task OnPerform(PlayerChoiceContext choiceContext)
    {
        ArgumentNullException.ThrowIfNull(CombatState);
        var candidates = CombatState.HittableEnemies.ToList();
        var blocked = candidates.Where(enemy => enemy.Block > 0).ToList();
        var target = Owner.RunState.Rng.CombatTargets.NextItem(blocked.Count > 0 ? blocked : candidates);
        if (target != null)
        {
            await CreatureCmd.LoseBlock(choiceContext, target, target.Block, Owner.Creature);

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, null)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(5);
    }
}