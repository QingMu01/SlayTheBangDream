using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;

namespace BangDreamInSpire.Scripts.Mechanics.VisualsAnim;

/// <summary>按生物串行、脱手且主线程推进的演出时间线，避免争抢视觉代理或阻塞动作队列。</summary>
public static class VisualsAnimTrack
{
    // 弱表按生物管理门，避免旧新时间线并发。
    private static readonly ConditionalWeakTable<Creature, SemaphoreSlim> Gates = new();

    private static readonly Lock GateLock = new();

    private static CancellationTokenSource _combatCancellation = new();

    /// <summary>提交演出后立即返回。</summary>
    public static void Submit(
        Creature creature,
        IReadOnlyList<IVisualsAnimStep> steps,
        IDisposable? completionLease = null)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(steps);

        TaskHelper.RunSafely(RunCoreAsync(creature, steps, default, completionLease));
    }

    /// <summary>战斗结束时取消演出并释放代理。</summary>
    public static void ResetCombat()
    {
        CancellationTokenSource previous;
        lock (GateLock)
        {
            previous = _combatCancellation;
            _combatCancellation = new CancellationTokenSource();
        }

        previous.Cancel();
        previous.Dispose();

        VisualsAnimProxyRegistry.Clear();
    }

    private static SemaphoreSlim GetGate(Creature creature) => Gates.GetOrCreateValue(creature);

    /// <summary>确保演出结束或异常时释放屏障租约。</summary>
    private static async Task RunCoreAsync(
        Creature creature,
        IReadOnlyList<IVisualsAnimStep> steps,
        CancellationToken cancellationToken,
        IDisposable? completionLease)
    {
        try
        {
            await RunTimelineAsync(creature, steps, cancellationToken);
        }
        finally
        {
            completionLease?.Dispose();
        }
    }

    private static async Task RunTimelineAsync(
        Creature creature,
        IReadOnlyList<IVisualsAnimStep> steps,
        CancellationToken cancellationToken)
    {
        var gate = GetGate(creature);

        CancellationTokenSource linked;
        lock (GateLock)
        {
            linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _combatCancellation.Token);
        }

        var token = linked.Token;
        VisualsAnimProxy? proxy = null;
        var acquired = false;

        try
        {
            await gate.WaitAsync(token);
            acquired = true;

            // 续体可能在线程池，必须回主线程继续，不能丢弃时间线。
            if (!NGame.IsMainThread())
            {
                BangDreamInSpireCore.Logger.Warn(
                    "VisualsAnim track resumed off the main thread; hopping back to the main thread.");
                await SwitchToMainThreadAsync(token);
            }

            proxy = VisualsAnimProxyRegistry.Acquire(creature);

            foreach (var step in steps)
            {
                token.ThrowIfCancellationRequested();
                await step.ExecuteAsync(proxy, token);
            }
        }
        catch (OperationCanceledException)
        {
            // 取消时静默收尾，代理由 ResetCombat 释放。
        }
        catch (Exception exception)
        {
            BangDreamInSpireCore.Logger.Error($"VisualsAnim track error: {exception}");
        }
        finally
        {
            VisualsAnimProxyRegistry.Release(proxy);

            if (acquired)
                gate.Release();

            linked.Dispose();
        }
    }

    /// <summary>通过延迟 Call 与主线程内联续体显式切回 Godot 主线程。</summary>
    private static async Task SwitchToMainThreadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var completion = new TaskCompletionSource<bool>();
        Callable.From(() => completion.TrySetResult(true)).CallDeferred();
        await completion.Task.WaitAsync(cancellationToken);
    }
}
