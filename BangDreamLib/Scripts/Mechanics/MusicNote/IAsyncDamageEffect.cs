namespace BangDreamLib.Scripts.Mechanics.MusicNote;

/// <summary>
/// 定义任意“启动动画后脱手，并在命中时刻结算伤害”的效果。
/// 实现无需继承指定 VFX 类型，只需提供命中时刻与结束任务。
/// </summary>
public interface IAsyncDamageEffect
{
    /// <summary>
    /// 在每个根动画启动前执行，用于播放施法动作或控制批次生成间隔。
    /// </summary>
    Task BeforeSpawnAsync(AsyncDamagePreparationContext context)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 计算该次命中的最终伤害。返回值在发射时刻锁定：既用于目标预留，也作为命中时实际施加的数值，
    /// 因此不受飞行期间的状态变化影响，保证多端一致。
    /// </summary>
    decimal GetLockedDamage(AsyncDamageTargetContext context);

    /// <summary>
    /// 启动动画并尽快返回句柄，不应在此等待整个动画结束。
    /// 句柄的 <see cref="AsyncDamageAnimationHandle.Landing" /> 必须在动画抵达目标时完成。
    /// </summary>
    Task<AsyncDamageAnimationHandle> StartAnimationAsync(AsyncDamageSpawnContext context);

    /// <summary>
    /// 在动画抵达目标后由当前同步 Action 调用，施加 <see cref="AsyncDamageHitContext.LockedDamage" />。
    /// </summary>
    Task ResolveDamageAsync(AsyncDamageHitContext context);

    Task AfterBatchAsync(AsyncDamageBatchContext context)
    {
        return Task.CompletedTask;
    }

    decimal GetTargetEffectiveHealth(AsyncDamageTargetContext context)
    {
        return Math.Max(0, context.Target.CurrentHp) + Math.Max(0, context.Target.Block);
    }
}
