using BangDreamInSpire.Scripts.Mechanics.AsyncDamage;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace BangDreamInSpire.Scripts.Mechanics.VisualsAnim;

/// <summary>
/// 卡片 / 能力侧的角色演出入口。所有步骤都相对「基准位置」表达（<see cref="VisualsAnimProxy.Home" />），
/// 因此同一套编排在不同站位、不同分辨率下都成立。
/// <para>
/// 典型用法（脱手）：
/// <code>
/// VisualsAnimCmd.Submit(Owner.Creature,
///     VisualsAnimCmd.Move(new Vector2(0f, -180f), 0.1f),
///     VisualsAnimCmd.Face(false),
///     VisualsAnimCmd.Callback(async ct => await MusicNoteCmd.FromCard(this, 3)),
///     VisualsAnimCmd.Reset());
/// </code>
/// </para>
/// </summary>
public static class VisualsAnimCmd
{
    /// <summary>提交演出后立即返回。</summary>
    public static void Submit(Creature creature, params IVisualsAnimStep[] steps)
    {
        VisualsAnimTrack.Submit(creature, steps, AcquireResolutionLease(creature, steps));
    }

    /// <summary>
    /// 含 <see cref="CallbackStep" /> 的演出承载玩法（例如推进到某处时发射音符），
    /// 必须租一个 <see cref="CombatResolutionBarrier" /> 租赁，否则玩家在演出中途结束回合时，
    /// 那些请求会被 `StartCancellingAllPlayerDrivenCombatActions` 取消，伤害静默丢失
    /// （即 docs/mechanic-authoring.md §13.4 记录过的边界）。
    /// <para>纯视觉演出不租，避免无谓地拖住回合结束。</para>
    /// </summary>
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

    /// <summary>相对基准位置做缓动位移。</summary>
    public static IVisualsAnimStep Move(Vector2 offsetFromHome, float seconds)
    {
        return new MoveStep(offsetFromHome, seconds);
    }

    /// <summary>相对基准位置瞬移。<paramref name="holdSeconds" /> 为落位后的停留时间（对应 STS1 <c>TpProcess</c> 的 duration）。</summary>
    public static IVisualsAnimStep Teleport(Vector2 offsetFromHome, float holdSeconds = 0f)
    {
        return new TeleportStep(offsetFromHome, holdSeconds);
    }

    /// <summary>演出节奏等待。</summary>
    public static IVisualsAnimStep Wait(float seconds)
    {
        return new WaitStep(seconds);
    }

    /// <summary>朝向：true 面向右，false 面向左。</summary>
    public static IVisualsAnimStep Face(bool right)
    {
        return new FaceStep(right);
    }

    /// <summary>绕角色纵轴自转 <paramref name="turns" /> 圈（STS1 <c>FlipProcess</c> 的等价实现）。</summary>
    public static IVisualsAnimStep Spin(int turns, float seconds)
    {
        return new SpinStep(turns, seconds);
    }

    /// <summary>回到基准位置与基准朝向。</summary>
    public static IVisualsAnimStep Reset()
    {
        return new ResetStep();
    }

    /// <summary>在演出推进到这个位置时执行逻辑（典型：发射一次异步伤害）。</summary>
    public static IVisualsAnimStep Callback(Func<CancellationToken, Task> callback)
    {
        return new CallbackStep(callback);
    }

    /// <summary>在演出推进到这个位置时执行同步逻辑。</summary>
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
