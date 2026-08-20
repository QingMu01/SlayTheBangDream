using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace BangDreamLib.Scripts.Features.AsyncDamage;

/// <summary>
/// 将异步伤害拆分为同步玩法结算和脱手表现层。玩法状态始终在调用方所属的同步 Action 内完成；
/// 动画启动后独立运行，动画信号不得再修改玩法状态。
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

    private readonly ConditionalWeakTable<ICombatState, CombatSession> _sessions = new();

    public static CombatAsyncDamageManager Shared { get; } = new();

    private CombatAsyncDamageManager()
    {
    }

    /// <summary>
    /// 启动整批动画，并在当前同步 Action 返回前完成全部玩法结算。不会等待动画命中或结束。
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

        VfxContext? lastRootContext = null;
        var launchedCount = 0;

        for (var index = 0; index < request.Count; index++)
        {
            if (!CanResolveCombat(combatState, request))
                break;

            await request.Effect.BeforeSpawnAsync(new AsyncDamagePreparationContext(
                combatState, request, index, request.Count));

            var root = await ResolveHitAsync(
                session, combatState, request, request.FixedTarget, excludedTarget: null,
                request.InitialVisualSource, index, request.Count, isChain: false, chainDepth: 0);
            if (root == null)
                break;

            lastRootContext = root.Value.Context;
            launchedCount++;

            var previousTarget = root.Value.Target;
            for (var chainDepth = 1; chainDepth <= Math.Max(0, request.ChainCount); chainDepth++)
            {
                if (!CanResolveCombat(combatState, request))
                    break;

                var chain = await ResolveHitAsync(
                    session, combatState, request, fixedTarget: null,
                    request.ExcludePreviousTargetFromChain ? previousTarget : null,
                    visualSource: previousTarget, index: 0, total: 1,
                    isChain: true, chainDepth);
                if (chain == null)
                    break;

                previousTarget = chain.Value.Target;
            }
        }

        if (lastRootContext != null && CanResolveCombat(combatState, request))
        {
            await request.Effect.AfterBatchAsync(new AsyncDamageBatchContext(
                combatState, request, lastRootContext, launchedCount));
        }

        if (CanResolveCombat(combatState, request))
            await CombatManager.Instance.CheckWinCondition();
    }

    private static async Task<(Creature Target, VfxContext? Context)?> ResolveHitAsync(
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

            if (reservation.Target.IsHittable && CanResolveCombat(combatState, request))
            {
                await request.Effect.ResolveDamageAsync(new AsyncDamageHitContext(
                    combatState, request, animation.Context, reservation.Target,
                    reservation.SequenceId, reservation.Damage, isChain, chainDepth));
            }

            return (reservation.Target, animation.Context);
        }
        finally
        {
            ReleaseReservation(session, reservation);
        }
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
            var estimatedDamage = Math.Max(0m, request.Effect.EstimateDamage(context));
            session.ReservedDamage.TryGetValue(target, out var currentReservation);
            session.ReservedDamage[target] = currentReservation + estimatedDamage;
            return new TargetReservation(target, estimatedDamage, session.NextSequenceId++);
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
