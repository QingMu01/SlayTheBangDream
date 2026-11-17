using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BangDreamInSpire.Scripts.Interfaces.Mechanics;
using BangDreamInSpire.Scripts.Mechanics.VisualsAnim;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace BangDreamInSpire.Scripts.Extensions;

/// <summary>
/// 角色演出时间线的**稳定扩展入口**。内容模组通过本类编排演出，不直接依赖 <c>VisualsAnimCmd</c>
/// 与步骤实现类型（见 <c>tools/check-architecture.py</c> 的 H1 规则）。
/// <para>
/// 用法：先用本类的工厂方法堆一个 <see cref="VisualsAnimStep" /> 列表，再用
/// <see cref="PlayVisualsAnim" /> 提交。提交即脱手，不占动作队列。
/// </para>
/// </summary>
public static class VisualsAnimExtensions
{
    /// <summary>相对基准位位移。</summary>
    public static VisualsAnimStep Move(Vector2 offsetFromHome, float seconds)
        => new(VisualsAnimCmd.Move(offsetFromHome, seconds));

    /// <summary>瞬移到相对基准位，并可保持若干秒。</summary>
    public static VisualsAnimStep Teleport(Vector2 offsetFromHome, float holdSeconds = 0f)
        => new(VisualsAnimCmd.Teleport(offsetFromHome, holdSeconds));

    /// <summary>原地等待。</summary>
    public static VisualsAnimStep Wait(float seconds)
        => new(VisualsAnimCmd.Wait(seconds));

    /// <summary>朝向翻转。</summary>
    public static VisualsAnimStep Face(bool right)
        => new(VisualsAnimCmd.Face(right));

    /// <summary>自转若干圈。</summary>
    public static VisualsAnimStep Spin(int turns, float seconds)
        => new(VisualsAnimCmd.Spin(turns, seconds));

    /// <summary>回到基准位与基准朝向。</summary>
    public static VisualsAnimStep Reset()
        => new(VisualsAnimCmd.Reset());

    /// <summary>逻辑步骤：无论有没有渲染都执行（headless / 测试下玩法不缺）。</summary>
    public static VisualsAnimStep Callback(Func<CancellationToken, Task> callback)
        => new(VisualsAnimCmd.Callback(callback));

    /// <summary>逻辑步骤的同步重载。</summary>
    public static VisualsAnimStep Callback(Action action)
        => new(VisualsAnimCmd.Callback(action));

    /// <summary>
    /// 提交一段演出：脱手推进、按生物串行，期间不占动作队列，玩家可继续出牌。
    /// </summary>
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
