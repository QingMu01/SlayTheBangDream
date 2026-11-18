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

    /// <summary>锁外锁定伤害期间目标失效时，重新选择目标的最大次数（有界重试，避免无限循环）。</summary>
    private const int MaxTargetSelectionAttempts = 4;

    public static CombatAsyncDamageManager Shared { get; } = new();

    private CombatAsyncDamageManager()
    {
    }

    /// <summary>
    /// 在同步 Action 内锁定整批根音符的目标与伤害后立即返回；飞行与命中由表现泵脱手驱动。
    /// 无交互模式（测试 / 无渲染）下退化为当场结算，保持原有确定性行为。
    /// <para>
    /// 锁边界：<see cref="Session.Gate" /> 内只做数据读写（批次/音符登记、预留账本）；
    /// 每枚音符的目标选择与 <see cref="IAsyncDamageEffect.GetLockedDamage" />
    /// 都在锁外调用（见 <see cref="ReserveTarget" />）。外部实现因此可以安全重入
    /// <c>LaunchAsync</c> / <c>SpawnAsync</c>，但不得在 hook 内操作 Godot 节点或长时间阻塞。
    /// </para>
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

        // 批次号分配必须串行；但除了取号，登记过程中不再持锁做任何外部调用。
        lock (session.Gate)
        {
            batch = new BatchState(session.NextBatchId++, request, combatState);
        }

        // 每枚音符的「选目标 + 锁定伤害」统一走 ReserveTarget：
        // 锁内只做数据快照与提交，GetTargetEffectiveHealth / GetLockedDamage 在锁外调用。
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

        // 屏障租约在锁外获取（不触碰节点），再回锁与批次一起提交，
        // 避免出现「批次已在 Batches 里可见、却还没有租约」的窗口。
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
    /// <para>
    /// <b>experimental / 预留入口</b>：当前没有任何调用点（见 docs/mechanic-authoring.md §7.6）。
    /// 接入前需要确认调用点本身是同步 Action，否则多端结算顺序无法保证。
    /// </para>
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
            BangDreamInSpireCore.Logger.Error($"Async damage inline animation error: {exception}");
        }
    }

    /// <summary>
    /// 施加单枚音符的已锁定伤害，并确定性推进其弹跳链；在同步 Action 内运行。
    /// </summary>
    private static async Task AdvanceAsync(Session session, NoteState note, bool spawnChains)
    {
        var batch = note.Batch;
        var request = batch.Request;

        // 伤害实现是外部代码（任意 IAsyncDamageEffect），抛异常不能连带中断下面的记账：
        // 一旦跳过归还，目标预留与批次在飞数都不会回收，CombatResolutionBarrier 的租约永不释放，
        // 回合结束会永久阻塞。先把异常收下来，记账照做，最后再记录。
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

        // 弹跳的目标选择同样要调用外部 hook，因此先在锁外完成「选目标 + 锁定伤害 + 登记」，
        // 再回锁把新音符入队并归还本音符的预留（保持「先预留下一枚、再归还本枚」的旧顺序）。
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

        // 脱手只到「第一个真正未完成的 await」为止：异步方法会同步执行到那里。
        // 因此 BeforeSpawnAsync / StartAnimationAsync 若同步完成，整批仍会留在调用方这个同步 Action 里。
        // 需要真正脱手的实现必须在首个 await 处让出（见 docs/mechanic-authoring.md §7.2）。
        _ = PumpAsync(session);
    }

    /// <summary>脱手表征泵：按注册顺序生成动画并观察命中，命中后请求同步结算。不占用同步 Action。</summary>
    private static async Task PumpAsync(Session session)
    {
        // 正在生成的那枚不在 SpawnQueue 里：泵异常退出时必须单独兜底，否则它会永久未结算。
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

        // 可能已由同步补算路径就地结算（FlushPendingAsync 是预留入口，当前无调用点），此时不再生成表现。
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
            // 表现生成失败不能只在本地静默丢弃伤害：若其它端照常命中，两端 HP 与目标预留会分叉。
            // 改走与正常命中相同的同步结算路径，让所有端在队列同一位置施加同一份已锁定伤害；
            // 网络层拒绝入队时由 RequestResolveOnArrival 兜底，不会让票据悬挂。
            RunOnMainThread(() => RequestResolveOnArrival(session, note));
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
            BangDreamInSpireCore.Logger.Warn(
                $"Async damage landing observation error #{note.NoteId}: {exception}");
            // Landing 抛异常（第三方 IAsyncDamageEffect 可能返回 faulted task）时不能让票据悬挂：
            // 按「已命中」处理，走结算/兜底路径，保证批次与屏障最终收敛。
            RunOnMainThread(() => RequestResolveOnArrival(session, note));
            return;
        }

        // 命中信号完成 TaskCompletionSource 时可能在线程池续体上恢复，切回主线程后再触碰动作队列。
        RunOnMainThread(() => RequestResolveOnArrival(session, note));
    }

    private static void RequestResolveOnArrival(Session session, NoteState note)
    {
        if (TryRequestResolve(session, note))
            return;

        // 网络层拒绝入队（断线 / 队列已关闭）：票据不能悬挂，本端就地结算兜底。
        // 代价是这一枚伤害丢失，但回合结束屏障可以完成 —— 优先保证不卡死。
        BangDreamInSpireCore.Logger.Error(
            $"Async damage resolve rejected by network layer, settling locally " +
            $"(batch={note.Batch.BatchId}, note={note.NoteId}).");
        TaskHelper.RunSafely(FailNoteAsync(session, note));
    }

    /// <summary>
    /// 请求一条同步结算 Action。返回 <c>false</c> 表示网络层拒绝入队（调用方需兜底）；
    /// 战斗已不允许结算、或本端不是发射者（由发射者负责请求）时返回 <c>true</c>。
    /// </summary>
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

    /// <summary>
    /// 为一次命中预留目标与锁定伤害。
    /// <para>
    /// <b>锁边界</b>：本方法自己按「快照（锁内）→ 外部 hook（锁外）→ 提交预留（锁内）」三段执行，
    /// 因此调用方<b>不得</b>包在 <c>lock (session.Gate)</c> 里调用，也不得在持有该锁时调用它。
    /// 锁内只做数据读写（候选列表、<see cref="Session.ReservedDamage" />），
    /// 绝不在持锁时调用外部实现或操作 Godot 节点。
    /// </para>
    /// <para>
    /// <b><c>GetTargetEffectiveHealth</c> / <c>GetLockedDamage</c> 的调用契约</b>：这两个是外部实现
    /// （任意 <see cref="IAsyncDamageEffect" />），在锁外被调用，因此实现里<b>不得</b>假设自己持有任何锁；
    /// 允许重入 <c>LaunchAsync</c> / <c>SpawnAsync</c>（不再有锁重入风险），但两者都在发射/弹跳的同步 Action 内执行，
    /// 不得长时间阻塞，也不要在这里操作节点（节点操作属于表现层，走 StartAnimationAsync）。
    /// </para>
    /// <para>
    /// 锁外窗口内没有 <c>await</c>，调用方都在主线程的同步 Action 里，因此不会与另一次选择交错；
    /// 若将来把调用方移到并行上下文，需要为候选选择再补一层在飞预留，否则并发选择可能超订同一目标。
    /// </para>
    /// </summary>
    private static (Creature Target, decimal Damage)? ReserveTarget(
        Session session,
        AsyncDamageBatchRequest request,
        Creature? fixedTarget,
        Creature? excludedTarget)
    {
        var combatState = request.Dealer.CombatState!;

        // 目标在锁外被击杀/离场/换战斗时重新选择；次数有界，绝不无限循环。
        for (var attempt = 0; attempt < MaxTargetSelectionAttempts; attempt++)
        {
            // 1) 锁内快照：只读状态，不做任何外部调用。
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
                // 2a) 锁外：容量口径是外部 hook，逐个候选调用。
                var capacities = new Dictionary<Creature, decimal>(ReferenceEqualityComparer.Instance);
                foreach (var candidate in candidates)
                {
                    capacities[candidate] = Math.Max(0m, request.Effect.GetTargetEffectiveHealth(
                        new AsyncDamageTargetContext(combatState, request, candidate)));
                }

                // 2b) 锁内：按预留账本与快照容量选目标（RNG 也在锁内，与旧实现同序）。
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

            // 2c) 锁外：锁定伤害是外部 hook。
            var lockedDamage = Math.Max(0m, request.Effect.GetLockedDamage(
                new AsyncDamageTargetContext(combatState, request, target)));

            // 3) 回锁提交：目标在锁外窗口内失效则记 Warn 并重新选择。
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

    /// <summary>
    /// 为已命中音符登记下一枚弹跳。本方法内部会走 <see cref="ReserveTarget" /> 的锁外外部调用，
    /// 因此调用方<b>不得</b>在持有 <see cref="Session.Gate" /> 时调用它；登记本身在锁内完成。
    /// </summary>
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
        // C-10 决策（C：保持现状）：dealer 无 Player 时每端都判定为本地 dealer。
        // 当前所有发射点（音符 / 能力）的 dealer 都是玩家，该分支不可达；若将来接入敌方式发射器，
        // 必须改为「固定宿主或按 dealer NetId」的确定性判定，否则会 N 端重复请求结算。
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