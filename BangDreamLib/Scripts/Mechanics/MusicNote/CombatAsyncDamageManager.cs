using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace BangDreamLib.Scripts.Mechanics.MusicNote;

/// <summary>
/// 编排「发射 → 等待命中 → 结算」，全过程运行在发起该批次的同步动作内。
/// 目标与伤害在发射时刻一次性锁定，飞行期间不再读取可变状态；玩法状态只在动作内变更，故多端顺序一致。
/// 表现层只提供命中时刻，不驱动玩法状态。
/// </summary>
public sealed class CombatAsyncDamageManager
{
    private sealed class CombatSession
    {
        public Lock SyncRoot { get; } = new();
        public Dictionary<Creature, decimal> ReservedDamage { get; } = new(ReferenceEqualityComparer.Instance);
        public Task BatchTail { get; set; } = Task.CompletedTask;
        public long NextSequenceId { get; set; }
    }

    private sealed class TargetReservation(Creature target, decimal damage, long sequenceId)
    {
        public Creature Target { get; } = target;
        public decimal Damage { get; } = damage;
        public long SequenceId { get; } = sequenceId;
        public bool IsReleased { get; set; }
    }

    private sealed class HitLaunch(
        TargetReservation reservation,
        AsyncDamageAnimationHandle animation,
        AsyncDamageSpawnContext spawnContext)
    {
        public TargetReservation Reservation { get; } = reservation;
        public AsyncDamageAnimationHandle Animation { get; } = animation;
        public AsyncDamageSpawnContext SpawnContext { get; } = spawnContext;
    }

    private readonly ConditionalWeakTable<ICombatState, CombatSession> _sessions = new();

    public static CombatAsyncDamageManager Shared { get; } = new();

    private CombatAsyncDamageManager()
    {
    }

    /// <summary>
    /// 发射整批动画，并在音符抵达目标后于当前同步动作内完成全部玩法结算。
    /// 调用方（出牌动作）只需入队本动作即可立即返回，不会被飞行阻塞。
    /// </summary>
    public Task SubmitAsync(AsyncDamageBatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Dealer);
        ArgumentNullException.ThrowIfNull(request.Effect);

        if (request.Count <= 0 || !TryGetCombatState(request, out var combatState))
            return Task.CompletedTask;

        var session = _sessions.GetValue(combatState, _ => new CombatSession());
        lock (session.SyncRoot)
        {
            var batch = ResolveAfterAsync(session.BatchTail, session, combatState, request);
            session.BatchTail = batch;
            return batch;
        }
    }

    private static async Task ResolveAfterAsync(
        Task previousBatch,
        CombatSession session,
        ICombatState combatState,
        AsyncDamageBatchRequest request)
    {
        try
        {
            await previousBatch;
        }
        catch (Exception exception)
        {
            BangDreamLibCore.Logger.Error($"Previous async damage batch error: {exception}");
        }

        try
        {
            await ResolveBatchAsync(session, combatState, request);
        }
        catch (Exception exception)
        {
            BangDreamLibCore.Logger.Error($"Async damage batch error: {exception}");
        }
    }

    private static async Task ResolveBatchAsync(
        CombatSession session,
        ICombatState combatState,
        AsyncDamageBatchRequest request)
    {
        // 阶段一：按节奏发射全部根音符，并在发射时刻锁定各自的目标与伤害。
        // 全部根音符在此阶段一起飞出，保证齐射观感。
        var launches = new List<HitLaunch>(request.Count);
        VfxContext? lastRootContext = null;
        var launchedCount = 0;

        for (var index = 0; index < request.Count; index++)
        {
            if (!CanResolveCombat(combatState, request))
                break;

            await request.Effect.BeforeSpawnAsync(new AsyncDamagePreparationContext(
                combatState, request, index, request.Count));

            var launch = await LaunchHitAsync(
                session, combatState, request, request.FixedTarget, excludedTarget: null,
                request.InitialVisualSource, index, request.Count, isChain: false, chainDepth: 0);
            if (launch == null)
                break;

            launches.Add(launch);
            lastRootContext = launch.Animation.Context;
            launchedCount++;
        }

        // 阶段二：按发射顺序等待命中并结算已锁定伤害。
        var chainRoots = new List<Creature>(launches.Count);
        var chainCount = Math.Max(0, request.ChainCount);
        foreach (var launch in launches)
        {
            if (!CanResolveCombat(combatState, request))
                break;

            var hitTarget = await ResolveLandingAsync(session, combatState, request, launch);
            if (hitTarget != null && chainCount > 0)
                chainRoots.Add(hitTarget);
        }

        // 阶段三：根音符全部命中后，再依次发起弹跳并递归结算。
        foreach (var chainRoot in chainRoots)
        {
            if (!CanResolveCombat(combatState, request))
                break;

            await RunChainsAsync(session, combatState, request, chainRoot, chainCount, chainDepth: 1);
        }

        if (lastRootContext != null && CanResolveCombat(combatState, request))
        {
            await request.Effect.AfterBatchAsync(new AsyncDamageBatchContext(
                combatState, request, lastRootContext, launchedCount));
        }

        if (CanResolveCombat(combatState, request))
            await CombatManager.Instance.CheckWinCondition();
    }

    private static async Task<HitLaunch?> LaunchHitAsync(
        CombatSession session,
        ICombatState combatState,
        AsyncDamageBatchRequest request,
        Creature? fixedTarget,
        Creature? excludedTarget,
        Creature? visualSource,
        int index,
        int total,
        bool isChain,
        int chainDepth)
    {
        var reservation = ReserveTarget(session, combatState, request, fixedTarget, excludedTarget);
        if (reservation == null)
            return null;

        var spawnContext = new AsyncDamageSpawnContext(
            combatState, request, reservation.Target, visualSource,
            reservation.SequenceId, index, total, isChain, chainDepth);

        try
        {
            var animation = await request.Effect.StartAnimationAsync(spawnContext);
            ArgumentNullException.ThrowIfNull(animation);
            _ = ObserveAnimationAsync(animation, reservation.SequenceId);
            return new HitLaunch(reservation, animation, spawnContext);
        }
        catch
        {
            ReleaseReservation(session, reservation);
            throw;
        }
    }

    /// <summary>
    /// 等待音符命中后施加已锁定伤害；目标在此期间死亡则对该次伤害丢弃（不重定向），以保持确定性。
    /// 返回命中确认时的目标（用于弹跳起点）；战斗不可继续或未命中时返回 null。
    /// </summary>
    private static async Task<Creature?> ResolveLandingAsync(
        CombatSession session,
        ICombatState combatState,
        AsyncDamageBatchRequest request,
        HitLaunch launch)
    {
        var target = launch.Reservation.Target;

        try
        {
            await launch.Animation.Landing;
            if (!CanResolveCombat(combatState, request))
                return null;

            if (target.IsHittable)
            {
                await request.Effect.ResolveDamageAsync(new AsyncDamageHitContext(
                    combatState, request, launch.Animation.Context, target,
                    launch.Reservation.SequenceId, launch.Reservation.Damage,
                    launch.SpawnContext.IsChain, launch.SpawnContext.ChainDepth));
            }

            return target;
        }
        finally
        {
            ReleaseReservation(session, launch.Reservation);
        }
    }

    /// <summary>
    /// 依次发起并结算弹跳音符，每枚在上一枚命中后才从被命中目标飞出。
    /// </summary>
    private static async Task RunChainsAsync(
        CombatSession session,
        ICombatState combatState,
        AsyncDamageBatchRequest request,
        Creature fromTarget,
        int remainingChains,
        int chainDepth)
    {
        if (remainingChains <= 0 || !CanResolveCombat(combatState, request))
            return;

        var chain = await LaunchHitAsync(
            session, combatState, request,
            fixedTarget: null,
            excludedTarget: request.ExcludePreviousTargetFromChain ? fromTarget : null,
            visualSource: fromTarget,
            index: 0, total: 1, isChain: true, chainDepth);
        if (chain == null)
            return;

        var hitTarget = await ResolveLandingAsync(session, combatState, request, chain);
        if (hitTarget == null)
            return;

        await RunChainsAsync(session, combatState, request, hitTarget, remainingChains - 1, chainDepth + 1);
    }

    private static async Task ObserveAnimationAsync(AsyncDamageAnimationHandle animation, long sequenceId)
    {
        try
        {
            await animation.Completion;
        }
        catch (Exception exception)
        {
            BangDreamLibCore.Logger.Warn(
                $"Detached async damage animation #{sequenceId} completion error: {exception}");
        }
    }

    private static TargetReservation? ReserveTarget(
        CombatSession session,
        ICombatState combatState,
        AsyncDamageBatchRequest request,
        Creature? fixedTarget,
        Creature? excludedTarget)
    {
        lock (session.SyncRoot)
        {
            Creature? target = null;
            if (fixedTarget != null)
            {
                if (!ReferenceEquals(fixedTarget, excludedTarget) &&
                    ReferenceEquals(fixedTarget.CombatState, combatState) &&
                    fixedTarget.IsHittable)
                {
                    target = fixedTarget;
                }
                else
                {
                    return null;
                }
            }

            if (target == null)
            {
                var candidates = combatState.HittableEnemies
                    .Where(candidate => !ReferenceEquals(candidate, excludedTarget))
                    .ToList();
                if (candidates.Count == 0)
                    return null;

                var selectable = candidates.Where(candidate =>
                {
                    var targetContext = new AsyncDamageTargetContext(combatState, request, candidate);
                    var capacity = Math.Max(0m, request.Effect.GetTargetEffectiveHealth(targetContext));
                    session.ReservedDamage.TryGetValue(candidate, out var reservedDamage);
                    return capacity - reservedDamage > 0m;
                }).ToList();
                if (selectable.Count == 0)
                    selectable = candidates;

                target = request.Dealer.Player?.RunState.Rng.CombatTargets.NextItem(selectable);
            }

            if (target == null)
                return null;

            var context = new AsyncDamageTargetContext(combatState, request, target);
            var lockedDamage = Math.Max(0m, request.Effect.GetLockedDamage(context));
            session.ReservedDamage.TryGetValue(target, out var currentReservation);
            session.ReservedDamage[target] = currentReservation + lockedDamage;
            return new TargetReservation(target, lockedDamage, session.NextSequenceId++);
        }
    }

    private static void ReleaseReservation(CombatSession session, TargetReservation reservation)
    {
        lock (session.SyncRoot)
        {
            if (reservation.IsReleased)
                return;

            reservation.IsReleased = true;
            if (!session.ReservedDamage.TryGetValue(reservation.Target, out var currentReservation))
                return;

            var remaining = currentReservation - reservation.Damage;
            if (remaining > 0m)
                session.ReservedDamage[reservation.Target] = remaining;
            else
                session.ReservedDamage.Remove(reservation.Target);
        }
    }

    private static bool TryGetCombatState(
        AsyncDamageBatchRequest request,
        [MaybeNullWhen(false)] out ICombatState combatState)
    {
        combatState = request.Dealer.CombatState;
        return combatState != null && CanResolveCombat(combatState, request);
    }

    private static bool CanResolveCombat(ICombatState combatState, AsyncDamageBatchRequest request)
    {
        return CombatManager.Instance.IsInProgress &&
               ReferenceEquals(request.Dealer.CombatState, combatState) &&
               request.Dealer.Player?.RunState != null;
    }
}
