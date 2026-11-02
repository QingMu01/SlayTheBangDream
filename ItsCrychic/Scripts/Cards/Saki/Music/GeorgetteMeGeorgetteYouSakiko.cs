using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.GameHook;
using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class GeorgetteMeGeorgetteYouSakiko() : AbstractSakikoMusicCard(CustomRarity, CustomTarget),
    IPerformHookListener
{
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private static readonly TargetType CustomTarget = PerformTargetTypes.RequestGroup;

    private bool _isRepeatingPerformance;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(5)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        // 奏响：基础版对所有敌人，升级版改为随机单体。由其他牌触发时（_isRepeatingPerformance）始终随机单体。
        if (IsUpgraded || _isRepeatingPerformance)
        {
            var target = Owner.RunState.Rng.CombatTargets.NextItem(CombatState.HittableEnemies);
            if (target == null) return;

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, null)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
            return;
        }

        foreach (var enemy in CombatState.HittableEnemies.ToList())
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, null)
                .Targeting(enemy)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }
    }

    public async Task OnCardPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        if (perform.Card == this || perform.Card.Owner != Owner || Pile?.Type != BangDreamConst.PerformPile ||
            perform.Card is GeorgetteMeGeorgetteYouSakiko || _isRepeatingPerformance) return;

        _isRepeatingPerformance = true;
        try
        {
            await Owner.AttachedData().PerformManager.PerformCard(this, true);
        }
        finally
        {
            _isRepeatingPerformance = false;
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
    }
}
