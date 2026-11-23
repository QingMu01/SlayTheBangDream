using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Nodes.VFX;
using BangDreamInSpire.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Combat.AttackHits;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class Surge() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), IAttackHitHookListener
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Ancient;
    private const TargetType CustomTarget = TargetType.AllEnemies;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.Lingered
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(10),
        QuickVar.LingeredResource.Create(3)
    ];

    private readonly List<Creature> _enemiesSnapshot = [];

    /// <summary>
    /// 只在本击真正的目标身上放命中幕：<c>WithHitVfxNode</c> 在多目标时对每个存活敌人都会建一次节点，
    /// 而此时 hook 收窄目标还没发生（见 docs/mechanic-authoring.md 12.4）。
    /// </summary>
    public Task BeforeAttackHit(AttackHitContext context)
    {
        if (context.CardSource != this || _enemiesSnapshot.Count == 0)
            return Task.CompletedTask;

        // 外部效果可增加命中段数，索引必须夹取以避免超出敌人快照。
        var index = Math.Min(context.HitIndex, _enemiesSnapshot.Count - 1);
        var target = _enemiesSnapshot[index];
        context.Targets = new List<Creature> { target };
        context.Damage *= (int)Math.Pow(2, index);

        var isFinal = context.HitIndex >= (int)context.TotalHitCount - 1;
        var echoCount = QuickVar.LingeredResource.GetVar(this).IntValue;
        var previous = index > 0 ? _enemiesSnapshot[index - 1] : null;
        var vfx = SurgeHitVfx.Create(target, Owner.Creature, previous, index, isFinal, echoCount);
        target.GetVfxContainer().AddChildSafely(vfx);

        return Task.CompletedTask;
    }

    /// <summary>余音在命中结算后入账：入账要走的溢出转换会拉长下一击的间隔，放在伤害之后才不会把波前拖离峰值。</summary>
    public async Task AfterAttackHit(AttackHitContext context)
    {
        if (context.CardSource != this)
            return;

        await SecondaryResourceCmd.Gain(Owner, BangDreamConst.LingeredResource,
            QuickVar.LingeredResource.GetVar(this).IntValue, this);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        _enemiesSnapshot.Clear();
        _enemiesSnapshot.AddRange(CombatState.HittableEnemies);
        if (_enemiesSnapshot.Count == 0)
            return;

        var hitCount = _enemiesSnapshot.Count;
        var animDelay = Owner.Character.AttackAnimDelay;
        var waveIndex = 0;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, play)
            .TargetingAllOpponents(CombatState)
            .WithHitCount(hitCount)
            .WithAttackerFx(() => CreateWave(waveIndex++, hitCount, animDelay))
            .WithWaitBeforeHit(SurgeWaveVfx.HitWaitFast, SurgeWaveVfx.HitWaitStandard)
            .Execute(choiceContext);
    }

    /// <summary>每击的传播幕：首击从演奏者起手，之后由上一个敌人弹出。</summary>
    private SurgeWaveVfx? CreateWave(int rawIndex, int hitCount, float animDelay)
    {
        if (_enemiesSnapshot.Count == 0)
            return null;

        var index = Math.Min(rawIndex, _enemiesSnapshot.Count - 1);
        var previous = index > 0 ? _enemiesSnapshot[index - 1] : null;
        return SurgeWaveVfx.Create(Owner.Creature, previous, _enemiesSnapshot[index], index, hitCount, animDelay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
