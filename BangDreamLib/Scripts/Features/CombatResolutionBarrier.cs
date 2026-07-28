using MegaCrit.Sts2.Core.Combat;

namespace BangDreamLib.Scripts.Features;

/// <summary>
/// 阻止战斗在异步结算效果完成前进入回合结束阶段。
/// </summary>
public static class CombatResolutionBarrier
{
    private sealed class BarrierState
    {
        public int LeaseCount { get; set; }
        public TaskCompletionSource<bool> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class BarrierLease(ICombatState combatState) : IDisposable
    {
        private ICombatState? _combatState = combatState;

        public void Dispose()
        {
            var state = Interlocked.Exchange(ref _combatState, null);
            if (state != null)
            {
                Release(state);
            }
        }
    }

    private static readonly Lock BarrierLock = new();
    private static readonly Dictionary<ICombatState, BarrierState> States = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// 为指定战斗注册一项尚未完成的异步结算。返回的 Lease 必须在结算结束时释放。
    /// </summary>
    public static IDisposable Acquire(ICombatState combatState)
    {
        ArgumentNullException.ThrowIfNull(combatState);

        lock (BarrierLock)
        {
            if (!States.TryGetValue(combatState, out var state))
            {
                state = new BarrierState();
                States[combatState] = state;
            }

            state.LeaseCount++;
        }

        return new BarrierLease(combatState);
    }

    /// <summary>
    /// 等待指定战斗的全部异步结算完成。
    /// </summary>
    public static Task WaitAsync(ICombatState combatState, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(combatState);

        Task completion;
        lock (BarrierLock)
        {
            if (!States.TryGetValue(combatState, out var state))
                return Task.CompletedTask;

            completion = state.Completion.Task;
        }

        return cancellationToken.CanBeCanceled
            ? completion.WaitAsync(cancellationToken)
            : completion;
    }

    public static bool HasPendingResolutions(ICombatState combatState)
    {
        ArgumentNullException.ThrowIfNull(combatState);

        lock (BarrierLock)
        {
            return States.ContainsKey(combatState);
        }
    }

    private static void Release(ICombatState combatState)
    {
        TaskCompletionSource<bool>? completion = null;
        lock (BarrierLock)
        {
            if (!States.TryGetValue(combatState, out var state))
                return;

            state.LeaseCount--;
            if (state.LeaseCount <= 0)
            {
                States.Remove(combatState);
                completion = state.Completion;
            }
        }

        completion?.TrySetResult(true);
    }
}
