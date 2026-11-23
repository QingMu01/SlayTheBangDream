using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using BangDreamInSpire.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;

namespace BangDreamInSpire.Scripts.Mechanics.AsyncDamage;

/// <summary>发射时锁定目标与伤害，命中后通过同步 Action 结算，保证多端顺序一致；
/// 无交互模式立即结算。</summary>
public sealed class CombatAsyncDamageManager
{
    private sealed class Session
    {
        public Lock Gate { get; } = new();
        public Dictionary<Creature, decimal> ReservedDamage { get; } = new(ReferenceEqualityComparer.Instance);
        public Dictionary<long, BatchState> Batches { get; } = new();
        public Dictionary<long, NoteState> Notes { get; } = new();
        public Queue<long> SpawnQueue { get; } = new();
        public long NextBatchId { get; set; }
        public long NextNoteId { get; set; }
        public bool PumpRunning { get; set; }
    }

    private sealed class BatchState(long batchId, AsyncDamageBatchRequest request, ICombatState combatState)
    {
        public long BatchId { get; } = batchId;
        public AsyncDamageBatchRequest Request { get; } = request;
        public ICombatState CombatState { get; } = combatState;
        public List<long> PendingNoteIds { get; } = [];
        public int PendingNotes { get; set; }
        public int LaunchedCount { get; set; }
        public VfxContext? LastRootContext { get; set; }
        public IDisposable? BarrierLease { get; set; }
        public bool Finished { get; set; }
    }

    private sealed class NoteState(
        long noteId,
        BatchState batch,
        Creature target,
        decimal lockedDamage,
        int chainsRemaining,
        int chainDepth,
        bool isChain,
        Creature? visualSource,
        int index,
        int total)
    {
        public long NoteId { get; } = noteId;
        public BatchState Batch { get; } = batch;
        public Creature Target { get; } = target;
        public decimal LockedDamage { get; } = lockedDamage;
        public int ChainsRemaining { get; } = chainsRemaining;
        public int ChainDepth { get; } = chainDepth;
        public bool IsChain { get; } = isChain;
        public Creature? VisualSource { get; } = visualSource;
        public int Index { get; } = index;
        public int Total { get; } = total;
        public VfxContext? Context { get; set; }
        public bool Resolved { get; set; }
        public bool ResolveRequested { get; set; }
    }

    private readonly ConditionalWeakTable<ICombatState, Session> _sessions = new();

    /// <summary>目标失效后的最大重选次数。</summary>
    private const int MaxTargetSelectionAttempts = 4;

    public static CombatAsyncDamageManager Shared { get; } = new();

    private CombatAsyncDamageManager()
    {
    }

    /// <summary>锁定整批目标与伤害后脱手。Session.Gate 仅做数据读写；
    /// 外部 hook 在锁外，可重入但不得操作节点或阻塞。</summary>
    public Task LaunchAsync(AsyncDamageBatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Dealer);
        ArgumentNullException.ThrowIfNull(request.Effect);

        if (request.Count <= 0 || !TryGetCombatState(request, out var combatState))
            return Task.CompletedTask;

        var session = _sessions.GetValue(combatState, _ => new Session());
        BatchState batch;
        var interactive = !NonInteractiveMode.IsActive;

        // 批次号在锁内分配，外部调用在锁外。
        lock (session.Gate)
        {
            batch = new BatchState(session.NextBatchId++, request, combatState);
        }

        for (var index = 0; index < request.Count; index++)
        {
            if (!CanResolveCombat(combatState, request))
                break;

            var reservation = ReserveTarget(session, request, request.FixedTarget, excludedTarget: null);
            if (reservation == null)
                break;

            lock (session.Gate)
            {
                var note = RegisterNote(
                    session, batch, reservation.Value.Target, reservation.Value.Damage,
                    request.ChainCount, chainDepth: 0, isChain: false, request.InitialVisualSource,
                    index, request.Count);
                batch.LaunchedCount++;
                if (interactive)
                    session.SpawnQueue.Enqueue(note.NoteId);
            }
        }

        lock (session.Gate)
        {
            if (batch.PendingNotes == 0)
                return Task.CompletedTask;
        }

        // 先获取屏障租约，再在锁内与批次一起提交。
        var barrierLease = CombatResolutionBarrier.Acquire(combatState);
        lock (session.Gate)
        {
            batch.BarrierLease = barrierLease;
            session.Batches[batch.BatchId] = batch;
        }

        if (!interactive)
            return ResolveInlineAsync(session, batch);

        EnsurePump(session);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 由独立的同步结算 Action 在所有端调用：施加已锁定伤害，并确定性推进弹跳或结束批次。
    /// </summary>
    public async Task ResolveAsync(ICombatState combatState, long batchId, long noteId)
    {
        if (!_sessions.TryGetValue(combatState, out var session))
            return;

        NoteState? note;
        lock (session.Gate)
        {
            if (!session.Batches.TryGetValue(batchId, out _) ||
                !session.Notes.TryGetValue(noteId, out note) ||
                note.Resolved)
                return;

            note.Resolved = true;
        }

        await AdvanceAsync(session, note, spawnChains: true);
    }

    /// <summary>战斗结束时清空在飞记录并完成屏障。</summary>
    public void OnCombatEnded(ICombatState combatState)
    {
        if (_sessions.TryGetValue(combatState, out var session))
        {
            lock (session.Gate)
            {
                foreach (var batch in session.Batches.Values)
                    batch.BarrierLease?.Dispose();

                session.Batches.Clear();
                session.Notes.Clear();
                session.SpawnQueue.Clear();
                session.PumpRunning = false;
            }

            _sessions.Remove(combatState);
        }

        CombatResolutionBarrier.ForceComplete(combatState);
    }

    /// <summary>在当前同步 Action 内就地结算全部在飞音符；调用前必须确保同步上下文。</summary>
    public async Task FlushPendingAsync(ICombatState combatState)
    {
        if (!_sessions.TryGetValue(combatState, out var session))
            return;

        while (true)
        {
            BatchState[] batches;
            lock (session.Gate)
            {
                batches = session.Batches.Values
                    .Where(batch => batch is { Finished: false, PendingNotes: > 0 })
                    .OrderBy(batch => batch.BatchId)
                    .ToArray();
            }

            if (batches.Length == 0)
                return;

            foreach (var batch in batches)
                await ResolveInlineAsync(session, batch);
        }
    }

    /// <summary>无表现时按注册顺序结算整个批次。</summary>
    private static async Task ResolveInlineAsync(Session session, BatchState batch)
    {
        while (true)
        {
            NoteState? next;
            lock (session.Gate)
            {
                if (!session.Batches.ContainsKey(batch.BatchId))
                    return;

                next = batch.PendingNoteIds
                    .Select(id => session.Notes.GetValueOrDefault(id))
                    .FirstOrDefault(note => note is { Resolved: false });
                if (next == null)
                    break;

                next.Resolved = true;
            }

            await SpawnForInlineResolutionAsync(next);
            await AdvanceAsync(session, next, spawnChains: false);
        }

        await FinishBatchAsync(session, batch);
    }

    /// <summary>生成上下文后立即结算，不等待命中。</summary>
    private static async Task SpawnForInlineResolutionAsync(NoteState note)
    {
        var batch = note.Batch;
        var request = batch.Request;
        try
        {
            await request.Effect.BeforeSpawnAsync(new AsyncDamagePreparationContext(
                batch.CombatState, request, note.Index, note.Total));
            var handle = await request.Effect.StartAnimationAsync(new AsyncDamageSpawnContext(
                batch.CombatState, request, note.Target, note.VisualSource,
                note.NoteId, note.Index, note.Total, note.IsChain, note.ChainDepth));
            note.Context = handle.Context;
            if (!note.IsChain && handle.Context != null)
                batch.LastRootContext = handle.Context;
        }
        catch (Exception exception)
        {
            BangDreamInSpireCore.Logger.Error($"Async damage inline animation error: {exception}");
        }
    }

    /// <summary>结算单枚音符并推进弹跳链。</summary>
    private static async Task AdvanceAsync(Session session, NoteState note, bool spawnChains)
    {
        var batch = note.Batch;
        var request = batch.Request;

        // 外部伤害实现异常也必须归还预留与在飞计数，否则屏障无法释放。
        Exception? resolveError = null;
        if (CanResolveCombat(batch.CombatState, request) && note.Target.IsHittable)
        {
            try
            {
                await request.Effect.ResolveDamageAsync(new AsyncDamageHitContext(
                    batch.CombatState, request, note.Context, note.Target,
                    note.NoteId, note.LockedDamage, note.IsChain, note.ChainDepth));
            }
            catch (Exception exception)
            {
                resolveError = exception;
            }
        }

        // 先预留下一枚，再归还本枚；外部 hook 始终在锁外。
        var chain = ReserveChainNote(session, note);

        bool finished;
        lock (session.Gate)
        {
            if (chain != null && spawnChains)
                session.SpawnQueue.Enqueue(chain.NoteId);

            ReleaseReservation(session, note.Target, note.LockedDamage);
            BatchReleaseResult(batch, out finished);
        }

        if (chain != null && spawnChains)
            EnsurePump(session);

        if (finished)
            await FinishBatchAsync(session, batch);

        if (resolveError != null)
        {
            BangDreamInSpireCore.Logger.Error(
                $"Async damage resolve error #{note.NoteId}: {resolveError}");
        }
    }

    private static async Task FinishBatchAsync(Session session, BatchState batch)
    {
        lock (session.Gate)
        {
            if (batch.Finished)
                return;

            batch.Finished = true;
            session.Batches.Remove(batch.BatchId);
        }

        batch.BarrierLease?.Dispose();
        batch.BarrierLease = null;

        var request = batch.Request;
        if (CanResolveCombat(batch.CombatState, request))
        {
            await request.Effect.AfterBatchAsync(new AsyncDamageBatchContext(
                batch.CombatState, request, batch.LastRootContext, batch.LaunchedCount));
        }

        if (CanResolveCombat(batch.CombatState, request))
            await CombatManager.Instance.CheckWinCondition();
    }

    private static void EnsurePump(Session session)
    {
        lock (session.Gate)
        {
            if (session.PumpRunning)
                return;

            session.PumpRunning = true;
        }

        // 异步方法仅执行到首个未完成 await；需要脱手的实现必须在该处让出。
        _ = PumpAsync(session);
    }

    /// <summary>按顺序生成表现，命中后请求同步结算。</summary>
    private static async Task PumpAsync(Session session)
    {
        // 正在生成的音符需单独兜底。
        NoteState? inFlight = null;
        try
        {
            while (true)
            {
                NoteState? note = null;
                lock (session.Gate)
                {
                    while (session.SpawnQueue.Count > 0)
                    {
                        var nextId = session.SpawnQueue.Dequeue();
                        if (session.Notes.TryGetValue(nextId, out var candidate) && !candidate.Resolved)
                        {
                            note = candidate;
                            break;
                        }
                    }

                    if (note == null)
                    {
                        session.PumpRunning = false;
                        return;
                    }
                }

                inFlight = note;
                await SpawnAsync(session, note);
                inFlight = null;
            }
        }
        catch (Exception exception)
        {
            BangDreamInSpireCore.Logger.Error($"Async damage presentation pump error: {exception}");
            lock (session.Gate)
                session.PumpRunning = false;

            if (inFlight is { Resolved: false } pending)
            {
                RunOnMainThread(() => RequestResolveOnArrival(session, pending));
            }
        }
        finally
        {
            // 泵失败时按无表现命中，避免屏障悬挂。
            await DrainQueueAsync(session);
        }
    }

    private static async Task DrainQueueAsync(Session session)
    {
        while (true)
        {
            NoteState? note = null;
            lock (session.Gate)
            {
                while (session.SpawnQueue.Count > 0)
                {
                    var nextId = session.SpawnQueue.Dequeue();
                    if (session.Notes.TryGetValue(nextId, out var candidate) && !candidate.Resolved)
                    {
                        note = candidate;
                        break;
                    }
                }
            }

            if (note == null)
                return;

            await FailNoteAsync(session, note);
        }
    }

    private static async Task SpawnAsync(Session session, NoteState note)
    {
        var batch = note.Batch;
        var request = batch.Request;
        if (!CanResolveCombat(batch.CombatState, request))
        {
            // 状态关闭也要推进批次，确保屏障完成。
            await FailNoteAsync(session, note);
            return;
        }

        // 已就地结算的批次不再生成表现。
        lock (session.Gate)
        {
            if (note.Resolved)
                return;
        }

        AsyncDamageAnimationHandle handle;
        try
        {
            await request.Effect.BeforeSpawnAsync(new AsyncDamagePreparationContext(
                batch.CombatState, request, note.Index, note.Total));

            var spawnContext = new AsyncDamageSpawnContext(
                batch.CombatState, request, note.Target, note.VisualSource,
                note.NoteId, note.Index, note.Total, note.IsChain, note.ChainDepth);
            handle = await request.Effect.StartAnimationAsync(spawnContext);
            ArgumentNullException.ThrowIfNull(handle);
        }
        catch (Exception exception)
        {
            BangDreamInSpireCore.Logger.Error($"Async damage animation launch error: {exception}");
            // 表现失败也走同步结算，避免各端伤害分叉。
            RunOnMainThread(() => RequestResolveOnArrival(session, note));
            return;
        }

        note.Context = handle.Context;
        if (!note.IsChain && handle.Context != null)
            batch.LastRootContext = handle.Context;

        _ = ObserveAnimationAsync(handle, note.NoteId);
        _ = ObserveArrivalAsync(session, note, handle);
    }

    private static async Task FailNoteAsync(Session session, NoteState note)
    {
        bool finished;
        lock (session.Gate)
        {
            if (note.Resolved)
                return;

            note.Resolved = true;
            ReleaseReservation(session, note.Target, note.LockedDamage);
            BatchReleaseResult(note.Batch, out finished);
        }

        if (finished)
            await FinishBatchAsync(session, note.Batch);
    }

    /// <summary>递减在飞数并返回批次是否结束。</summary>
    private static void BatchReleaseResult(BatchState batch, out bool finished)
    {
        batch.PendingNotes--;
        finished = batch.PendingNotes <= 0;
    }

    private static async Task ObserveArrivalAsync(Session session, NoteState note, AsyncDamageAnimationHandle handle)
    {
        try
        {
            await handle.Landing;
        }
        catch (Exception exception)
        {
            BangDreamInSpireCore.Logger.Warn(
                $"Async damage landing observation error #{note.NoteId}: {exception}");
            // Landing 失败按命中兜底，避免票据悬挂。
            RunOnMainThread(() => RequestResolveOnArrival(session, note));
            return;
        }

        // 命中续体可能在线程池，先切回主线程。
        RunOnMainThread(() => RequestResolveOnArrival(session, note));
    }

    private static void RequestResolveOnArrival(Session session, NoteState note)
    {
        if (TryRequestResolve(session, note))
            return;

        // 入队失败时就地兜底，优先保证屏障完成。
        BangDreamInSpireCore.Logger.Error(
            $"Async damage resolve rejected by network layer, settling locally " +
            $"(batch={note.Batch.BatchId}, note={note.NoteId}).");
        TaskHelper.RunSafely(FailNoteAsync(session, note));
    }

    /// <summary>请求同步结算；false 表示入队被拒，非发射者或战斗关闭时无需请求。</summary>
    private static bool TryRequestResolve(Session session, NoteState note)
    {
        var request = note.Batch.Request;
        if (!CanResolveCombat(note.Batch.CombatState, request))
            return true;

        if (!IsLocalDealer(request.Dealer.Player))
            return true;

        lock (session.Gate)
        {
            if (note.Resolved || note.ResolveRequested)
                return true;

            note.ResolveRequested = true;
        }

        return AsyncDamageResolveCmd.RequestResolve(note.Batch.BatchId, note.NoteId, request.Dealer.Player);
    }

    private static void RunOnMainThread(Action action)
    {
        if (NGame.IsMainThread())
        {
            action();
            return;
        }

        Callable.From(action).CallDeferred();
    }

    private static async Task ObserveAnimationAsync(AsyncDamageAnimationHandle handle, long noteId)
    {
        try
        {
            await handle.Completion;
        }
        catch (Exception exception)
        {
            BangDreamInSpireCore.Logger.Warn(
                $"Detached async damage animation #{noteId} completion error: {exception}");
        }
    }

    private static NoteState RegisterNote(
        Session session,
        BatchState batch,
        Creature target,
        decimal lockedDamage,
        int chainsRemaining,
        int chainDepth,
        bool isChain,
        Creature? visualSource,
        int index,
        int total)
    {
        var noteId = session.NextNoteId++;
        var note = new NoteState(
            noteId, batch, target, lockedDamage, chainsRemaining, chainDepth, isChain, visualSource, index, total);
        session.Notes[noteId] = note;
        batch.PendingNoteIds.Add(noteId);
        batch.PendingNotes++;
        return note;
    }

    /// <summary>按“锁内快照 → 锁外 hook → 锁内提交”预留目标与伤害。
    /// 调用方不得持有 Session.Gate；外部 hook 可重入，但不得阻塞或操作节点。
    /// 当前调用均在主线程同步 Action 内，锁外窗口无 await。</summary>
    private static (Creature Target, decimal Damage)? ReserveTarget(
        Session session,
        AsyncDamageBatchRequest request,
        Creature? fixedTarget,
        Creature? excludedTarget)
    {
        var combatState = request.Dealer.CombatState!;

        // 目标失效时有界重试。
        for (var attempt = 0; attempt < MaxTargetSelectionAttempts; attempt++)
        {
            // 锁内快照，不调用外部代码。
            Creature[] candidates;
            Creature? fixedCandidate = null;
            lock (session.Gate)
            {
                if (!CanResolveCombat(combatState, request))
                    return null;

                if (fixedTarget != null)
                {
                    if (!ReferenceEquals(fixedTarget, excludedTarget) &&
                        ReferenceEquals(fixedTarget.CombatState, combatState) &&
                        fixedTarget.IsHittable)
                    {
                        fixedCandidate = fixedTarget;
                    }
                    else
                    {
                        return null;
                    }
                }

                candidates = fixedCandidate != null
                    ? [fixedCandidate]
                    : combatState.HittableEnemies
                        .Where(candidate => !ReferenceEquals(candidate, excludedTarget))
                        .ToArray();
            }

            if (candidates.Length == 0)
                return null;

            Creature? target;
            if (fixedCandidate != null)
            {
                target = fixedCandidate;
            }
            else
            {
                // 锁外读取候选容量。
                var capacities = new Dictionary<Creature, decimal>(ReferenceEqualityComparer.Instance);
                foreach (var candidate in candidates)
                {
                    capacities[candidate] = Math.Max(0m, request.Effect.GetTargetEffectiveHealth(
                        new AsyncDamageTargetContext(combatState, request, candidate)));
                }

                // 锁内选择目标并推进 RNG。
                lock (session.Gate)
                {
                    var selectable = candidates.Where(candidate =>
                    {
                        session.ReservedDamage.TryGetValue(candidate, out var reservedDamage);
                        return capacities[candidate] - reservedDamage > 0m;
                    }).ToList();
                    if (selectable.Count == 0)
                        selectable = [.. candidates];

                    target = request.Dealer.Player?.RunState.Rng.CombatTargets.NextItem(selectable);
                }

                if (target == null)
                    return null;
            }

            // 锁外锁定伤害。
            var lockedDamage = Math.Max(0m, request.Effect.GetLockedDamage(
                new AsyncDamageTargetContext(combatState, request, target)));

            // 回锁提交；目标失效则重试。
            lock (session.Gate)
            {
                if (ReferenceEquals(target.CombatState, combatState) && target.IsHittable)
                {
                    session.ReservedDamage.TryGetValue(target, out var currentReservation);
                    session.ReservedDamage[target] = currentReservation + lockedDamage;
                    return (target, lockedDamage);
                }
            }

            BangDreamInSpireCore.Logger.Warn(
                $"Async damage target {target} became invalid while locking damage " +
                $"(dealer={request.Dealer}, attempt={attempt + 1}/{MaxTargetSelectionAttempts}); reselecting.");
        }

        BangDreamInSpireCore.Logger.Warn(
            $"Async damage gave up reserving a target after {MaxTargetSelectionAttempts} attempts " +
            $"(dealer={request.Dealer}, fixedTarget={fixedTarget?.ToString() ?? "-"}).");
        return null;
    }

    /// <summary>登记下一枚弹跳；调用方不得持有 Session.Gate。</summary>
    private static NoteState? ReserveChainNote(Session session, NoteState note)
    {
        var request = note.Batch.Request;
        if (note.ChainsRemaining <= 0 || !CanResolveCombat(note.Batch.CombatState, request))
            return null;

        var excluded = request.ExcludePreviousTargetFromChain ? note.Target : null;
        var reservation = ReserveTarget(session, request, fixedTarget: null, excluded);
        if (reservation == null)
            return null;

        lock (session.Gate)
        {
            return RegisterNote(
                session, note.Batch, reservation.Value.Target, reservation.Value.Damage,
                note.ChainsRemaining - 1, note.ChainDepth + 1, isChain: true, note.Target,
                index: 0, total: 1);
        }
    }

    private static void ReleaseReservation(Session session, Creature target, decimal damage)
    {
        if (!session.ReservedDamage.TryGetValue(target, out var currentReservation))
            return;

        var remaining = currentReservation - damage;
        if (remaining > 0m)
            session.ReservedDamage[target] = remaining;
        else
            session.ReservedDamage.Remove(target);
    }

    private static bool IsLocalDealer(Player? dealer)
    {
        // 敌方式发射器尚无归属端；接入前必须固定宿主或 dealer NetId，避免重复结算。
        if (dealer == null || !LocalContext.NetId.HasValue)
            return true;

        return LocalContext.IsMe(dealer);
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