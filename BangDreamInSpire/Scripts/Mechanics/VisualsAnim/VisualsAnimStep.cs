using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace BangDreamInSpire.Scripts.Mechanics.VisualsAnim;

/// <summary>演出步骤；视觉步骤在无代理时跳过，逻辑步骤始终执行。</summary>
public interface IVisualsAnimStep
{
    Task ExecuteAsync(VisualsAnimProxy? proxy, CancellationToken cancellationToken);
}

internal static class VisualsAnimEnv
{
    /// <summary>瞬间模式直接到终态。</summary>
    public static bool InstantMode
    {
        get
        {
            var prefs = SaveManager.Instance?.PrefsSave;
            return prefs != null && prefs.FastMode == FastModeType.Instant;
        }
    }
}

/// <summary>等待 Tween 完成；Kill 不发 Finished，因此取消与退树也必须完成任务。</summary>
internal static class TweenAwaiter
{
    public static async Task AwaitAsync(Tween tween, Node lifetimeOwner, CancellationToken cancellationToken)
    {
        // 续体留在主线程。
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

/// <summary>相对基准位置缓动。</summary>
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

/// <summary>瞬移并停留，无渲染时不等待。</summary>
public sealed class TeleportStep(Vector2 offsetFromHome, float holdSeconds) : IVisualsAnimStep
{
    public async Task ExecuteAsync(VisualsAnimProxy? proxy, CancellationToken cancellationToken)
    {
        if (proxy != null && proxy.IsAlive)
            proxy.Position = proxy.Home + offsetFromHome;

        if (proxy == null || holdSeconds <= 0f)
            return;

        await Cmd.Wait(holdSeconds, cancellationToken);
    }
}

/// <summary>等待指定时间，无渲染时跳过。</summary>
public sealed class WaitStep(float seconds) : IVisualsAnimStep
{
    public async Task ExecuteAsync(VisualsAnimProxy? proxy, CancellationToken cancellationToken)
    {
        if (proxy == null)
            return;

        await Cmd.Wait(seconds, cancellationToken);
    }
}

/// <summary>设置左右朝向。</summary>
public sealed class FaceStep(bool right) : IVisualsAnimStep
{
    public Task ExecuteAsync(VisualsAnimProxy? proxy, CancellationToken cancellationToken)
    {
        proxy?.Face(right);
        return Task.CompletedTask;
    }
}

/// <summary>用横向缩放模拟纵轴自转。</summary>
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

/// <summary>恢复基准位置与朝向。</summary>
public sealed class ResetStep : IVisualsAnimStep
{
    public Task ExecuteAsync(VisualsAnimProxy? proxy, CancellationToken cancellationToken)
    {
        proxy?.Reset();
        return Task.CompletedTask;
    }
}

/// <summary>执行逻辑回调，无渲染时也执行。</summary>
public sealed class CallbackStep(Func<CancellationToken, Task> callback) : IVisualsAnimStep
{
    public Task ExecuteAsync(VisualsAnimProxy? proxy, CancellationToken cancellationToken)
    {
        return callback(cancellationToken);
    }
}
