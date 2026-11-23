using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;

namespace BangDreamInSpire.Scripts.Mechanics.AsyncDamage;

/// <summary>跟踪未完成的异步伤害，回合结束前等待全部批次完成。</summary>
public static class CombatResolutionBarrier
{
    private sealed class BarrierState
    {
        public int LeaseCount { get; set; }

        /// <summary>续体在主线程内联执行；TrySetResult 在锁外调用，避免重入。</summary>
        public TaskCompletionSource<bool> Completion { get; } = new();
    }

    private sealed class BarrierLease(ICombatState combatState) : IDisposable
    {
        private ICombatState? _combatState = combatState;

        public void Dispose()
        {
            var state = Interlocked.Exchange(ref _combatState, null);
            if (state != null)
                Release(state);
        }
    }

    private static readonly Lock BarrierLock = new();

    /// <summary>按对象标识弱引用战斗状态，避免延长生命周期。</summary>
    private static readonly ConditionalWeakTable<ICombatState, BarrierState> States = new();

    /// <summary>登记异步结算并返回结束时释放的租赁。</summary>
    public static IDisposable Acquire(ICombatState combatState)
    {
        ArgumentNullException.ThrowIfNull(combatState);

        lock (BarrierLock)
        {
            var state = States.GetValue(combatState, static _ => new BarrierState());
            state.LeaseCount++;
        }

        return new BarrierLease(combatState);
    }

    /// <summary>等待指定战斗结算完成。</summary>
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

    /// <summary>战斗结束时强制完成等待。</summary>
    public static void ForceComplete(ICombatState combatState)
    {
        ArgumentNullException.ThrowIfNull(combatState);

        TaskCompletionSource<bool>? completion;
        lock (BarrierLock)
        {
            if (!States.TryGetValue(combatState, out var state))
                return;

            States.Remove(combatState);
            completion = state.Completion;
        }

        completion.TrySetResult(true);
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
