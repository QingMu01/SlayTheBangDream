using BangDreamInSpire.Scripts.Mechanics.AsyncDamage;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace BangDreamInSpire.Scripts.Mechanics.VisualsAnim;

/// <summary>角色演出入口，步骤相对基准位置编排。</summary>
public static class VisualsAnimCmd
{
    /// <summary>提交演出后立即返回。</summary>
    public static void Submit(Creature creature, params IVisualsAnimStep[] steps)
    {
        VisualsAnimTrack.Submit(creature, steps, AcquireResolutionLease(creature, steps));
    }

    /// <summary>含逻辑回调的演出必须持有回合屏障，纯视觉演出不持有。</summary>
    private static IDisposable? AcquireResolutionLease(Creature creature, IReadOnlyList<IVisualsAnimStep> steps)
    {
        var hasGameplaySteps = false;
        foreach (var step in steps)
        {
            if (step is CallbackStep)
            {
                hasGameplaySteps = true;
                break;
            }
        }

        if (!hasGameplaySteps)
            return null;

        var combatState = creature.CombatState;
        return combatState == null ? null : CombatResolutionBarrier.Acquire(combatState);
    }

    /// <summary>相对基准位置缓动。</summary>
    public static IVisualsAnimStep Move(Vector2 offsetFromHome, float seconds)
    {
        return new MoveStep(offsetFromHome, seconds);
    }

    /// <summary>相对基准位置瞬移并停留。</summary>
    public static IVisualsAnimStep Teleport(Vector2 offsetFromHome, float holdSeconds = 0f)
    {
        return new TeleportStep(offsetFromHome, holdSeconds);
    }

    /// <summary>等待指定时间。</summary>
    public static IVisualsAnimStep Wait(float seconds)
    {
        return new WaitStep(seconds);
    }

    /// <summary>设置朝向，true 向右。</summary>
    public static IVisualsAnimStep Face(bool right)
    {
        return new FaceStep(right);
    }

    /// <summary>绕纵轴自转指定圈数。</summary>
    public static IVisualsAnimStep Spin(int turns, float seconds)
    {
        return new SpinStep(turns, seconds);
    }

    /// <summary>恢复基准位置与朝向。</summary>
    public static IVisualsAnimStep Reset()
    {
        return new ResetStep();
    }

    /// <summary>执行异步逻辑回调。</summary>
    public static IVisualsAnimStep Callback(Func<CancellationToken, Task> callback)
    {
        return new CallbackStep(callback);
    }

    /// <summary>执行同步逻辑回调。</summary>
    public static IVisualsAnimStep Callback(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new CallbackStep(_ =>
        {
            action();
            return Task.CompletedTask;
        });
    }
}
