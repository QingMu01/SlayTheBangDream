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
    private static readonly Dictionary<Creature, SemaphoreSlim> Gates = new(ReferenceEqualityComparer.Instance);

    private static readonly Lock GateLock = new();

    private static CancellationTokenSource _combatCancellation = new();

    /// <summary>提交一条演出并等待它跑完。会按生物排队。</summary>
    public static Task RunAsync(
        Creature creature,
        IReadOnlyList<IVisualsAnimStep> steps,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(steps);

        return RunCoreAsync(creature, steps, cancellationToken);
    }

    /// <summary>提交一条演出后立即返回，不阻塞调用方（典型用法：在 <c>OnPlay</c> 里提交）。</summary>
    public static void Submit(Creature creature, IReadOnlyList<IVisualsAnimStep> steps)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(steps);

        TaskHelper.RunSafely(RunCoreAsync(creature, steps, default));
    }

    /// <summary>战斗结束：取消所有在跑的演出并释放代理，避免回合或节点生命周期被悬挂的时间线拖住。</summary>
    public static void ResetCombat()
    {
        CancellationTokenSource previous;
        lock (GateLock)
        {
            previous = _combatCancellation;
            _combatCancellation = new CancellationTokenSource();
            Gates.Clear();
        }

        previous.Cancel();
        previous.Dispose();

        VisualsAnimProxyRegistry.Clear();
    }

    private static SemaphoreSlim GetGate(Creature creature)
    {
        lock (GateLock)
        {
            if (!Gates.TryGetValue(creature, out var gate))
            {
                gate = new SemaphoreSlim(1, 1);
                Gates[creature] = gate;
            }

            return gate;
        }
    }

    private static async Task RunCoreAsync(
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

            // 时间线必须全程在主线程推进：tween 信号、节点操作都在主线程，
            // 若续体被调度到线程池会直接崩在 Godot 的节点访问上。
            if (!NGame.IsMainThread())
            {
                BangDreamInSpireCore.Logger.Error(
                    "VisualsAnim track resumed off the main thread; aborting this animation.");
                return;
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
}
