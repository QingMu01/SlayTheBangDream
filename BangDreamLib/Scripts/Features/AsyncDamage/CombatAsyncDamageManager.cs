using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace BangDreamLib.Scripts.Features.AsyncDamage;

/// <summary>
/// 统一管理脱手异步伤害动画的目标预留、命中顺序、链式效果和战斗结算生命周期。
/// </summary>
public sealed class CombatAsyncDamageManager
{
    private sealed class CombatSession
    {
        public Lock SyncRoot { get; } = new();
        public Dictionary<Creature, decimal> ReservedDamage { get; } = new(ReferenceEqualityComparer.Instance);
        public Task ResolutionTail { get; set; } = Task.CompletedTask;
        public long NextSequenceId { get; set; }
        public int ActiveBatches { get; set; }
    }

    private sealed class TargetReservation(Creature target, decimal damage, long sequenceId)
    {
        public Creature Target { get; } = target;
        public decimal Damage { get; } = damage;
        public long SequenceId { get; } = sequenceId;
        public bool IsReleased { get; set; }
    }

    private sealed class PendingDamage(
        AsyncDamageBatchRequest request,
        AsyncDamageSpawnContext spawnContext,
        AsyncDamageAnimationHandle animation,
        TargetReservation reservation)
    {
        public AsyncDamageBatchRequest Request { get; } = request;
        public AsyncDamageSpawnContext SpawnContext { get; } = spawnContext;
        public AsyncDamageAnimationHandle Animation { get; } = animation;
        public TargetReservation Reservation { get; set; } = reservation;
        public Creature? ResolvedTarget { get; set; }
        public bool ReachedImpact { get; set; }
    }

    private readonly ConditionalWeakTable<ICombatState, CombatSession> _sessions = new();

    public static CombatAsyncDamageManager Shared { get; } = new();

    private CombatAsyncDamageManager()
    {
    }

    public async Task SubmitAsync(AsyncDamageBatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Dealer);
        ArgumentNullException.ThrowIfNull(request.Effect);

        if (request.Count <= 0 || !TryGetCombatState(request, out var combatState))
            return;

        var session = _sessions.GetValue(combatState, _ => new CombatSession());
        RegisterBatch(session);
        using var resolutionLease = CombatResolutionBarrier.Acquire(combatState);
        try
        {
            await ResolveBatchAsync(session, combatState, request);
        }
        catch (Exception exception)
        {
            BangDreamLibCore.Logger.Error($"Async damage batch resolution error: {exception}");
        }
        finally
        {
            if (UnregisterBatch(session))
                await CompleteCombatResolutionAsync(session, combatState, request);
        }
    }

    private async Task ResolveBatchAsync(
        CombatSession session,
        ICombatState combatState,
        AsyncDamageBatchRequest request)
    {
        var rootAnimations = new List<Task>(request.Count);
        VfxContext? lastRootContext = null;
        var launchedCount = 0;

        try
        {
            for (var index = 0; index < request.Count; index++)
            {
                if (!CanResolveCombat(combatState, request))
                    break;

                await request.Effect.BeforeSpawnAsync(new AsyncDamagePreparationContext(
                    combatState,
                    request,
                    index,
                    request.Count));

                var launch = await CreateAnimationAsync(
                    session,
                    combatState,
                    request,
                    request.FixedTarget,
                    excludedTarget: null,
                    visualSource: request.InitialVisualSource,
                    index,
                    request.Count,
                    isChain: false,
                    chainDepth: 0);
                if (launch == null)
                    break;

                lastRootContext = launch.Value.Animation.Context;
                launchedCount++;
                rootAnimations.Add(RunAnimationAsync(
                    session,
                    combatState,
                    request,
                    launch.Value,
                    Math.Max(0, request.ChainCount)));
            }
        }
        catch (Exception exception)
        {
            BangDreamLibCore.Logger.Error($"Async damage animation launch error: {exception}");
        }

        if (rootAnimations.Count == 0)
            return;

        await Task.WhenAll(rootAnimations);

        if (CanResolveCombat(combatState, request))
        {
            var batchContext = new AsyncDamageBatchContext(
                combatState,
                request,
                lastRootContext,
                launchedCount);
            await CombatEffectQueue.Shared.Enqueue(() => request.Effect.AfterBatchAsync(batchContext));
        }
    }

    private async Task<(AsyncDamageAnimationHandle Animation, AsyncDamageSpawnContext SpawnContext,
        TargetReservation Reservation)?> CreateAnimationAsync(
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
            combatState,
            request,
            reservation.Target,
            visualSource,
            reservation.SequenceId,
            index,
            total,
            isChain,
            chainDepth);

        try
        {
            var animation = await request.Effect.StartAnimationAsync(spawnContext);
            ArgumentNullException.ThrowIfNull(animation);
            return (animation, spawnContext, reservation);
        }
        catch
        {
            ReleaseReservation(session, reservation);
            throw;
        }
    }

    private async Task RunAnimationAsync(
        CombatSession session,
        ICombatState combatState,
        AsyncDamageBatchRequest request,
        (AsyncDamageAnimationHandle Animation, AsyncDamageSpawnContext SpawnContext,
            TargetReservation Reservation) launch,
        int remainingChains)
    {
        var pending = new PendingDamage(request, launch.SpawnContext, launch.Animation, launch.Reservation);
        await ScheduleResolution(session, combatState, pending);

        try
        {
            await launch.Animation.Completion;
        }
        catch (Exception exception)
        {
            BangDreamLibCore.Logger.Warn($"Async damage animation completion error: {exception}");
        }

        if (remainingChains <= 0 || !pending.ReachedImpact || !CanResolveCombat(combatState, request))
            return;

        var previousTarget = pending.ResolvedTarget ?? launch.Reservation.Target;
        var excludedTarget = request.ExcludePreviousTargetFromChain ? previousTarget : null;
        var chainLaunch = await CreateAnimationAsync(
            session,
            combatState,
            request,
            fixedTarget: null,
            excludedTarget,
            visualSource: previousTarget,
            index: 0,
            total: 1,
            isChain: true,
            chainDepth: launch.SpawnContext.ChainDepth + 1);
        if (chainLaunch == null)
            return;

        await RunAnimationAsync(
            session,
            combatState,
            request,
            chainLaunch.Value,
            remainingChains - 1);
    }

    private Task ScheduleResolution(
        CombatSession session,
        ICombatState combatState,
        PendingDamage pending)
    {
        lock (session.SyncRoot)
        {
            var previous = session.ResolutionTail;
            var resolution = ResolveAfterAsync(previous, session, combatState, pending);
            session.ResolutionTail = resolution;
            return resolution;
        }
    }

    private async Task ResolveAfterAsync(
        Task previous,
        CombatSession session,
        ICombatState combatState,
        PendingDamage pending)
    {
        try
        {
            await previous;
        }
        catch (Exception exception)
        {
            BangDreamLibCore.Logger.Error($"Previous async damage resolution error: {exception}");
        }

        try
        {
            var animationResult = await pending.Animation.Impact;
            if (animationResult != AsyncDamageAnimationResult.Triggered ||
                !CanResolveCombat(combatState, pending.Request))
            {
                return;
            }

            pending.ReachedImpact = true;
            await CombatEffectQueue.Shared.Enqueue(async () =>
            {
                if (!CanResolveCombat(combatState, pending.Request))
                    return;

                var target = pending.Reservation.Target;
                if (!target.IsHittable)
                {
                    if (pending.Request.TargetPolicy != AsyncDamageTargetPolicy.RetargetOnInvalid)
                        return;

                    var replacement = ReplaceReservation(session, combatState, pending, target);
                    if (replacement == null)
                        return;

                    target = replacement.Target;
                }

                pending.ResolvedTarget = target;
                var hitContext = new AsyncDamageHitContext(
                    combatState,
                    pending.Request,
                    pending.Animation.Context,
                    target,
                    pending.Reservation.SequenceId,
                    pending.Reservation.Damage,
                    pending.SpawnContext.IsChain,
                    pending.SpawnContext.ChainDepth);
                await pending.Request.Effect.ResolveDamageAsync(hitContext);
            });
        }
        catch (Exception exception)
        {
            BangDreamLibCore.Logger.Error($"Async damage impact resolution error: {exception}");
        }
        finally
        {
            ReleaseReservation(session, pending.Reservation);
        }
    }

    private TargetReservation? ReplaceReservation(
        CombatSession session,
        ICombatState combatState,
        PendingDamage pending,
        Creature excludedTarget)
    {
        ReleaseReservation(session, pending.Reservation);
        var replacement = ReserveTarget(
            session,
            combatState,
            pending.Request,
            fixedTarget: null,
            excludedTarget: excludedTarget,
            sequenceId: pending.Reservation.SequenceId);
        if (replacement != null)
            pending.Reservation = replacement;
        return replacement;
    }

    private TargetReservation? ReserveTarget(
        CombatSession session,
        ICombatState combatState,
        AsyncDamageBatchRequest request,
        Creature? fixedTarget,
        Creature? excludedTarget,
        long? sequenceId = null)
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
                else if (request.TargetPolicy != AsyncDamageTargetPolicy.RetargetOnInvalid)
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

                var selectable = candidates;
                if (request.TargetPolicy != AsyncDamageTargetPolicy.AllowOverkill)
                {
                    selectable = candidates.Where(candidate =>
                    {
                        var targetContext = new AsyncDamageTargetContext(combatState, request, candidate);
                        var capacity = Math.Max(0m, request.Effect.GetTargetEffectiveHealth(targetContext));
                        session.ReservedDamage.TryGetValue(candidate, out var reservedDamage);
                        return capacity - reservedDamage > 0m;
                    }).ToList();

                    if (selectable.Count == 0)
                        selectable = candidates;
                }

                target = request.Dealer.Player?.RunState.Rng.CombatTargets.NextItem(selectable);
            }

            if (target == null)
                return null;

            var context = new AsyncDamageTargetContext(combatState, request, target);
            var estimatedDamage = Math.Max(0m, request.Effect.EstimateDamage(context));
            session.ReservedDamage.TryGetValue(target, out var currentReservation);
            session.ReservedDamage[target] = currentReservation + estimatedDamage;
            return new TargetReservation(target, estimatedDamage, sequenceId ?? session.NextSequenceId++);
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

    private static void RegisterBatch(CombatSession session)
    {
        lock (session.SyncRoot)
        {
            session.ActiveBatches++;
        }
    }

    private static bool UnregisterBatch(CombatSession session)
    {
        lock (session.SyncRoot)
        {
            session.ActiveBatches = Math.Max(0, session.ActiveBatches - 1);
            return session.ActiveBatches == 0;
        }
    }

    private static bool HasActiveBatches(CombatSession session)
    {
        lock (session.SyncRoot)
        {
            return session.ActiveBatches > 0;
        }
    }

    private static async Task CompleteCombatResolutionAsync(
        CombatSession session,
        ICombatState combatState,
        AsyncDamageBatchRequest request)
    {
        await CombatEffectQueue.Shared.Enqueue(async () =>
        {
            if (!CanResolveCombat(combatState, request) || HasActiveBatches(session))
                return;

            await CombatManager.Instance.CheckWinCondition();
        });
    }

    private static bool TryGetCombatState(AsyncDamageBatchRequest request, [MaybeNullWhen(false)] out ICombatState combatState)
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