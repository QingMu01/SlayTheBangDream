using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class FaceSakiko() : AbstractSakikoMusicCard(CustomRarity, CustomTarget)
{
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private static readonly TargetType CustomTarget = PerformTargetTypes.RequestGroup;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(13)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        // 落位目标与伤害目标独立，后者随机选取敌人。
        var target = Owner.RunState.Rng.CombatTargets.NextItem(CombatState.HittableEnemies);
        if (target != null)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, null)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        // 本牌在歌单，满足 CreateClone 的战斗牌堆前提。
        var clone = CreateClone();
        await CardPileCmd.AddGeneratedCardToCombat(clone, BangDreamConst.ExtraDraw, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4);
    }
}
