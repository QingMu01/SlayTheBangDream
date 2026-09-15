using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using BangDreamLib.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;

namespace BangDreamLib.Scripts.Mechanics.AsyncDamage;

/// <summary>
/// 编排「发射 → 等待命中 → 结算」的异步伤害引擎。
/// 发射阶段只做确定性锁定（目标与伤害）后立即返回，飞行由脱手的表现泵驱动，不占用同步 Action；
/// 命中时刻由表现信号触发，入队一条独立的同步结算 Action，所有端按队列顺序在其内施加已锁定伤害。
/// 玩法状态只在同步 Action 内变更，故多端顺序一致。
/// </summary>
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

    public static CombatAsyncDamageManager Shared { get; } = new();

    private CombatAsyncDamageManager()
    {
    }

    /// <summary>
    /// 在同步 Action 内锁定整批根音符的目标与伤害后立即返回；飞行与命中由表现泵脱手驱动。
    /// 无交互模式（测试 / 无渲染）下退化为当场结算，保持原有确定性行为。
    /// </summary>
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

        lock (session.Gate)
        {
            batch = new BatchState(session.NextBatchId++, request, combatState);
            for (var index = 0; index < request.Count; index++)
            {
                if (!CanResolveCombat(combatState, request))
                    break;

                var reservation = ReserveTarget(session, request, request.FixedTarget, excludedTarget: null);
                if (reservation == null)
                    break;

                var note = RegisterNote(
                    session, batch, reservation.Value.Target, reservation.Value.Damage,
                    request.ChainCount, chainDepth: 0, isChain: false, request.InitialVisualSource,
                    index, request.Count);
                batch.LaunchedCount++;
                if (interactive)
                    session.SpawnQueue.Enqueue(note.NoteId);
            }

            if (batch.PendingNotes == 0)
                return Task.CompletedTask;

            session.Batches[batch.BatchId] = batch;
            batch.BarrierLease = CombatResolutionBarrier.Acquire(combatState);
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

    /// <summary>战斗结束：清空该战斗的全部在飞记录并强制完成屏障。由机制在战斗结束事件中调用。</summary>
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

    /// <summary>
    /// 就地结算该战斗当前所有在飞音符（含弹跳），用于需要立即读取完整伤害记录的调用方。
    /// 运行于调用方所属的同步 Action 内，故多端看到相同的结算集合与顺序。
    /// </summary>
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

    /// <summary>无表现路径：按注册顺序就地结算批次内全部音符与弹跳，随后收尾。</summary>
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

    /// <summary>
    /// 无交互模式下的退化表现：仍走一次动画启动以获得上下文（保证批末回调语义不变），但不等待命中，立即结算。
    /// </summary>
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
            BangDreamLibCore.Logger.Error($"Async damage inline animation error: {exception}");
        }
    }

    /// <summary>
    /// 施加单枚音符的已锁定伤害，并确定性推进其弹跳链；在同步 Action 内运行。
    /// </summary>
    private static async Task AdvanceAsync(Session session, NoteState note, bool spawnChains)
    {
        var batch = note.Batch;
        var request = batch.Request;

        if (CanResolveCombat(batch.CombatState, request) && note.Target.IsHittable)
        {
            await request.Effect.ResolveDamageAsync(new AsyncDamageHitContext(
                batch.CombatState, request, note.Context, note.Target,
                note.NoteId, note.LockedDamage, note.IsChain, note.ChainDepth));
        }

        NoteState? chain;
        bool finished;
        lock (session.Gate)
        {
            chain = ReserveChainNote(session, note);
            if (chain != null && spawnChains)
                session.SpawnQueue.Enqueue(chain.NoteId);

            ReleaseReservation(session, note.Target, note.LockedDamage);
            BatchReleaseResult(batch, out finished);
        }

        if (chain != null && spawnChains)
            EnsurePump(session);

        if (finished)
            await FinishBatchAsync(session, batch);
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

        _ = PumpAsync(session);
    }

    /// <summary>脱手表征泵：按注册顺序生成动画并观察命中，命中后请求同步结算。不占用同步 Action。</summary>
    private static async Task PumpAsync(Session session)
    {
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

                await SpawnAsync(session, note);
            }
        }
        catch (Exception exception)
        {
            BangDreamLibCore.Logger.Error($"Async damage presentation pump error: {exception}");
            lock (session.Gate)
                session.PumpRunning = false;
        }
        finally
        {
            // 泵异常退出时，仍在队列中的音符会悬挂并阻塞回合结束屏障，统一按无表现命中退化落地。
            await DrainQueueAsync(session);
        }
    }

    /// <summary>把表现泵队列中尚未生成的音符全部按失败兜底落地，避免悬挂。</summary>
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
            // 战斗状态不再允许结算时也必须推进批次，否则在飞记录悬挂会让回合结束屏障永不完成。
            await FailNoteAsync(session, note);
            return;
        }

        // 可能已被 FlushPendingAsync 就地结算（例如 Finale/Moonlight 打牌时强制落地），此时不再生成表现。
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
            BangDreamLibCore.Logger.Error($"Async damage animation launch error: {exception}");
            // 表现生成失败时不能留下悬挂的在飞记录（否则回合结束屏障永不完成），按无表现命中退化落地。
            await FailNoteAsync(session, note);
            return;
        }

        note.Context = handle.Context;
        if (!note.IsChain && handle.Context != null)
            batch.LastRootContext = handle.Context;

        _ = ObserveAnimationAsync(handle, note.NoteId);
        _ = ObserveArrivalAsync(session, note, handle);
    }

    /// <summary>表现生成失败时的兜底结算：释放预留并推进批次，保证屏障最终完成。</summary>
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

    /// <summary>在锁内递减批次在飞数并返回是否已全部结束。</summary>
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
            BangDreamLibCore.Logger.Warn(
                $"Async damage landing observation error #{note.NoteId}: {exception}");
            return;
        }

        // 命中信号完成 TaskCompletionSource 时可能在线程池续体上恢复，切回主线程后再触碰动作队列。
        RunOnMainThread(() => RequestResolveOnArrival(session, note));
    }

    private static void RequestResolveOnArrival(Session session, NoteState note)
    {
        var request = note.Batch.Request;
        if (!CanResolveCombat(note.Batch.CombatState, request))
            return;

        if (!IsLocalDealer(request.Dealer.Player))
            return;

        lock (session.Gate)
        {
            if (note.Resolved || note.ResolveRequested)
                return;

            note.ResolveRequested = true;
        }

        AsyncDamageResolveCmd.RequestResolve(note.Batch.BatchId, note.NoteId, request.Dealer.Player);
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
            BangDreamLibCore.Logger.Warn(
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

    /// <summary>为一次命中预留目标与锁定伤害。必须在 <see cref="Session.Gate" /> 内调用。</summary>
    private static (Creature Target, decimal Damage)? ReserveTarget(
        Session session,
        AsyncDamageBatchRequest request,
        Creature? fixedTarget,
        Creature? excludedTarget)
    {
        var combatState = request.Dealer.CombatState!;
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
        return (target, lockedDamage);
    }

    /// <summary>为已命中音符登记下一枚弹跳。必须在 <see cref="Session.Gate" /> 内调用。</summary>
    private static NoteState? ReserveChainNote(Session session, NoteState note)
    {
        var request = note.Batch.Request;
        if (note.ChainsRemaining <= 0 || !CanResolveCombat(note.Batch.CombatState, request))
            return null;

        var excluded = request.ExcludePreviousTargetFromChain ? note.Target : null;
        var reservation = ReserveTarget(session, request, fixedTarget: null, excluded);
        if (reservation == null)
            return null;

        return RegisterNote(
            session, note.Batch, reservation.Value.Target, reservation.Value.Damage,
            note.ChainsRemaining - 1, note.ChainDepth + 1, isChain: true, note.Target,
            index: 0, total: 1);
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