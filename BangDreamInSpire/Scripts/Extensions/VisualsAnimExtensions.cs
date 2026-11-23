using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BangDreamInSpire.Scripts.Interfaces.Mechanics;
using BangDreamInSpire.Scripts.Mechanics.VisualsAnim;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace BangDreamInSpire.Scripts.Extensions;

/// <summary>内容模组的演出扩展入口，提交后脱手并按生物串行。</summary>
public static class VisualsAnimExtensions
{
    /// <summary>相对基准位置缓动。</summary>
    public static VisualsAnimStep Move(Vector2 offsetFromHome, float seconds)
        => new(VisualsAnimCmd.Move(offsetFromHome, seconds));

    /// <summary>瞬移并停留。</summary>
    public static VisualsAnimStep Teleport(Vector2 offsetFromHome, float holdSeconds = 0f)
        => new(VisualsAnimCmd.Teleport(offsetFromHome, holdSeconds));

    /// <summary>等待指定时间。</summary>
    public static VisualsAnimStep Wait(float seconds)
        => new(VisualsAnimCmd.Wait(seconds));

    /// <summary>设置朝向。</summary>
    public static VisualsAnimStep Face(bool right)
        => new(VisualsAnimCmd.Face(right));

    /// <summary>自转指定圈数。</summary>
    public static VisualsAnimStep Spin(int turns, float seconds)
        => new(VisualsAnimCmd.Spin(turns, seconds));

    /// <summary>恢复基准位置与朝向。</summary>
    public static VisualsAnimStep Reset()
        => new(VisualsAnimCmd.Reset());

    /// <summary>逻辑步骤，无渲染时也执行。</summary>
    public static VisualsAnimStep Callback(Func<CancellationToken, Task> callback)
        => new(VisualsAnimCmd.Callback(callback));

    /// <summary>同步逻辑步骤。</summary>
    public static VisualsAnimStep Callback(Action action)
        => new(VisualsAnimCmd.Callback(action));

    /// <summary>提交演出。</summary>
    public static void PlayVisualsAnim(this Creature creature, params VisualsAnimStep[] steps)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(steps);

        var inner = new List<IVisualsAnimStep>(steps.Length);
        foreach (var step in steps)
        {
            inner.Add(step.Inner);
        }

        VisualsAnimCmd.Submit(creature, [.. inner]);
    }
}
