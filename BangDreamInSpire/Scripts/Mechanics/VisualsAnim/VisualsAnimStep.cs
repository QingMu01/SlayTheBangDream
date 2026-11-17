using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace BangDreamInSpire.Scripts.Mechanics.VisualsAnim;

/// <summary>
/// 演出时间线上的一步。视觉步骤在无渲染环境（<paramref name="proxy" /> 为 null）下必须自行跳过，
/// 逻辑步骤（如发射异步伤害）无论有没有表现都要执行，否则 headless / 测试下玩法会缺失。
/// </summary>
public interface IVisualsAnimStep
{
    Task ExecuteAsync(VisualsAnimProxy? proxy, CancellationToken cancellationToken);
}

/// <summary>演出步骤的公共环境判定。</summary>
internal static class VisualsAnimEnv
{
    /// <summary>「瞬间模式」下所有视觉步骤直接落到终态，与 <see cref="Cmd.Wait" /> 的跳过行为保持一致。</summary>
    public static bool InstantMode
    {
        get
        {
            var prefs = SaveManager.Instance?.PrefsSave;
            return prefs != null && prefs.FastMode == FastModeType.Instant;
        }
    }
}

/// <summary>
/// 把 Godot <see cref="Tween" /> 的完成事件转成可 await 的 Task，并且保证**任何清理路径都会完成它**。
/// <para>
/// <see cref="Tween" /> 被 <c>Kill()</c> 时不会发 <see cref="Tween.SignalName.Finished" />，
/// 直接 <c>await</c> 原始信号会永久挂起并卡死整条演出（进而卡死回合）。这里在取消与节点退出时都强制完成。
/// </para>
/// </summary>
internal static class TweenAwaiter
{
    public static async Task AwaitAsync(Tween tween, Node lifetimeOwner, CancellationToken cancellationToken)
    {
        // 不使用 RunContinuationsAsynchronously：完成方（tween 信号 / 节点退出）都在主线程，
        // 这样后续步骤的续体也留在主线程，避免跨线程触碰 Godot 节点。
        var completion = new TaskCompletionSource();

        void OnFinished()
        {
            completion.TrySetResult();
        }

        void OnTreeExited()
        {
            completion.TrySetResult();
        }

        tween.Finished += OnFinished;
        lifetimeOwner.TreeExited += OnTreeExited;

        using var registration = cancellationToken.Register(() =>
        {
            if (GodotObject.IsInstanceValid(tween) && tween.IsValid())
                tween.Kill();
            completion.TrySetCanceled(cancellationToken);
        });

        try
        {
            await completion.Task;
        }
        finally
        {
            if (GodotObject.IsInstanceValid(tween))
                tween.Finished -= OnFinished;
            if (GodotObject.IsInstanceValid(lifetimeOwner))
                lifetimeOwner.TreeExited -= OnTreeExited;
        }
    }
}

/// <summary>在基准位置的基础上做缓动位移。</summary>
public sealed class MoveStep(Vector2 offsetFromHome, float seconds, float delaySeconds = 0f) : IVisualsAnimStep
{
    public async Task ExecuteAsync(VisualsAnimProxy? proxy, CancellationToken cancellationToken)
    {
        if (proxy == null || !proxy.IsAlive)
            return;

        var target = proxy.Home + offsetFromHome;
        if (seconds <= 0f || VisualsAnimEnv.InstantMode)
        {
            proxy.Position = target;
            return;
        }

        var from = proxy.Position;
        var tween = proxy.Holder.CreateTween();
        if (delaySeconds > 0f)
            tween.TweenInterval(delaySeconds);
        tween.TweenMethod(
            Callable.From<Vector2>(value =>
            {
                if (proxy.IsAlive)
                    proxy.Position = value;
            }),
            from,
            target,
            seconds);

        await TweenAwaiter.AwaitAsync(tween, proxy.Holder, cancellationToken);
    }
}

/// <summary>
/// 瞬移到基准位置 + 偏移，不产生中间帧。
/// <paramref name="holdSeconds" /> 表达「落位后停留」，对应 STS1 <c>TpProcess(x, y, duration)</c>
/// 的 duration 语义——那边是先到位、再把 duration 当停留时间递减，而不是一段过渡。
/// </summary>
public sealed class TeleportStep(Vector2 offsetFromHome, float holdSeconds) : IVisualsAnimStep
{
    public async Task ExecuteAsync(VisualsAnimProxy? proxy, CancellationToken cancellationToken)
    {
        if (proxy != null && proxy.IsAlive)
            proxy.Position = proxy.Home + offsetFromHome;

        // 停留属于纯表现节奏：无渲染环境不等待，与 <see cref="WaitStep" /> 一致。
        if (proxy == null || holdSeconds <= 0f)
            return;

        await Cmd.Wait(holdSeconds, cancellationToken);
    }
}

/// <summary>等待一段时间（表现节奏）。无渲染环境不等待。</summary>
public sealed class WaitStep(float seconds) : IVisualsAnimStep
{
    public async Task ExecuteAsync(VisualsAnimProxy? proxy, CancellationToken cancellationToken)
    {
        if (proxy == null)
            return;

        await Cmd.Wait(seconds, cancellationToken);
    }
}

/// <summary>改变朝向（左右镜像）。</summary>
public sealed class FaceStep(bool right) : IVisualsAnimStep
{
    public Task ExecuteAsync(VisualsAnimProxy? proxy, CancellationToken cancellationToken)
    {
        proxy?.Face(right);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 绕角色纵轴自转若干圈。STS1 <c>FlipProcess</c>（<c>rotateY</c> 0→360°）的等价实现，
/// 2D 里靠横向缩放的 <c>cos</c> 得到「侧面收窄 → 背面镜像 → 转回正面」的观感。
/// </summary>
public sealed class SpinStep(int turns, float seconds) : IVisualsAnimStep
{
    public async Task ExecuteAsync(VisualsAnimProxy? proxy, CancellationToken cancellationToken)
    {
        if (proxy == null || !proxy.IsAlive)
            return;

        if (seconds <= 0f || VisualsAnimEnv.InstantMode)
        {
            proxy.SetSpin(0f);
            return;
        }

        var tween = proxy.Holder.CreateTween();
        tween.TweenMethod(
            Callable.From<float>(radians =>
            {
                if (proxy.IsAlive)
                    proxy.SetSpin(radians);
            }),
            0f,
            Mathf.Tau * turns,
            seconds);

        await TweenAwaiter.AwaitAsync(tween, proxy.Holder, cancellationToken);
        proxy.SetSpin(0f);
    }
}

/// <summary>回到基准位置与基准朝向。</summary>
public sealed class ResetStep : IVisualsAnimStep
{
    public Task ExecuteAsync(VisualsAnimProxy? proxy, CancellationToken cancellationToken)
    {
        proxy?.Reset();
        return Task.CompletedTask;
    }
}

/// <summary>
/// 逻辑步骤：在演出推进到这个位置时执行任意逻辑（典型用法是发射一次异步伤害）。
/// 无渲染环境下照常执行，保证玩法不缺。
/// </summary>
public sealed class CallbackStep(Func<CancellationToken, Task> callback) : IVisualsAnimStep
{
    public Task ExecuteAsync(VisualsAnimProxy? proxy, CancellationToken cancellationToken)
    {
        return callback(cancellationToken);
    }
}
