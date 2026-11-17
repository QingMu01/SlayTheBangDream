using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;

namespace BangDreamInSpire.Scripts.Mechanics.VisualsAnim;

/// <summary>
/// 角色演出的时间线：**脱手**（不占动作队列）且**按生物串行**。
/// <para>
/// 串行是刻意的：同一时刻只有一个身体，两个演出叠加会抢同一个视觉代理。串行的代价是连打两张演出牌时，
/// 第二张的整条时间线（含它的伤害）排在第一次之后——与 STS1 <c>AbstractRenderer.effectList</c> 的行为一致。
/// </para>
/// <para>
/// 脱手是设计目标而不是优化：动作队列被占住时所有玩家都不能出牌（<c>NPlayerHand</c> 在任意玩家驱动动作
/// 执行期间会 <c>AnimDisable</c>），演出必须在队列之外自己跑。
/// </para>
/// <para>
/// 时间线在主线程推进（tween 信号、<see cref="Godot.Tween" /> 完成、节点退出都在主线程），
/// 步骤之间不跨线程。
/// </para>
/// </summary>
public static class VisualsAnimTrack
{
    // 弱表：门只随生物存活，不在战斗结束时清空 —— 清空会让旧时间线未退出时新时间线直接并发。
    private static readonly ConditionalWeakTable<Creature, SemaphoreSlim> Gates = new();

    private static readonly Lock GateLock = new();

    private static CancellationTokenSource _combatCancellation = new();

    /// <summary>提交一条演出并等待它跑完。会按生物排队。</summary>
    public static Task RunAsync(
        Creature creature,
        IReadOnlyList<IVisualsAnimStep> steps,
        CancellationToken cancellationToken = default,
        IDisposable? completionLease = null)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(steps);

        return RunCoreAsync(creature, steps, cancellationToken, completionLease);
    }

    /// <summary>提交一条演出后立即返回，不阻塞调用方（典型用法：在 <c>OnPlay</c> 里提交）。</summary>
    public static void Submit(
        Creature creature,
        IReadOnlyList<IVisualsAnimStep> steps,
        IDisposable? completionLease = null)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(steps);

        TaskHelper.RunSafely(RunCoreAsync(creature, steps, default, completionLease));
    }

    /// <summary>战斗结束：取消所有在跑的演出并释放代理，避免回合或节点生命周期被悬挂的时间线拖住。</summary>
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

    /// <summary>
    /// 时间线外壳：只负责保证 <paramref name="completionLease" />（回合结束屏障租赁）一定被释放，
    /// 即使建立令牌源或推演过程中抛异常也不会让屏障悬挂。
    /// </summary>
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

            // 时间线必须全程在主线程推进：tween 信号、节点操作都在主线程。
            // 信号量竞争后的续体可能落在线程池 —— 此时**不能丢弃**整条时间线：
            // 那样连 Callback 里的玩法请求（伤害）都会静默消失，与「按生物串行、排在后面」的承诺相反。
            // 改为显式回主线程再继续。
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
            // 战斗结束 / 主动取消：静默收尾，代理由 ResetCombat 统一释放。
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

    /// <summary>
    /// 把续体送回 Godot 主线程。本项目没有安装 <c>SynchronizationContext</c>，
    /// <c>await</c> 在线程池上完成的 Task 时会落回线程池，因此用「延迟调用 + 非内联 TCS」显式切换。
    /// TCS 刻意不带 <c>RunContinuationsAsynchronously</c>：要让续体在主线程内联执行。
    /// </summary>
    private static async Task SwitchToMainThreadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var completion = new TaskCompletionSource<bool>();
        Callable.From(() => completion.TrySetResult(true)).CallDeferred();
        await completion.Task.WaitAsync(cancellationToken);
    }
}
