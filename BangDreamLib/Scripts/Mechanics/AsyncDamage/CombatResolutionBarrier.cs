using MegaCrit.Sts2.Core.Combat;

namespace BangDreamLib.Scripts.Mechanics.AsyncDamage;

/// <summary>
/// 追踪一场战斗中尚未结算完成的异步伤害批次，阻止战斗在它们全部落地前进入回合结束阶段。
/// 每个批次在发射时 <see cref="Acquire" /> 一个租赁，在批次内全部音符（含弹跳）结算完毕时释放；
/// 战斗结束时由机制统一 <see cref="ForceComplete" />，避免租赁悬挂。
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
                Release(state);
        }
    }

    private static readonly Lock BarrierLock = new();
    private static readonly Dictionary<ICombatState, BarrierState> States = new(ReferenceEqualityComparer.Instance);

    /// <summary>为指定战斗登记一项尚未完成的异步结算。返回的租赁必须在结算结束时释放。</summary>
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

    /// <summary>等待指定战斗的全部异步结算完成。</summary>
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

    /// <summary>战斗结束时强制清空该战斗的等待，防止已释放的结算永远阻塞回合推进。</summary>
    public static void ForceComplete(ICombatState combatState)
    {
        ArgumentNullException.ThrowIfNull(combatState);

        TaskCompletionSource<bool>? completion;
        lock (BarrierLock)
        {
            if (!States.Remove(combatState, out var state))
                return;

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
