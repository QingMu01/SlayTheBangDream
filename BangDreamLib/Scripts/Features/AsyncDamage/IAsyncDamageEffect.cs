namespace BangDreamLib.Scripts.Features.AsyncDamage;

/// <summary>
/// 定义任意“启动动画后脱手，并在稍后时刻造成伤害”的效果。
/// 实现无需继承指定 VFX 类型，只需提供动画命中与结束任务。
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

    decimal EstimateDamage(AsyncDamageTargetContext context);

    /// <summary>
    /// 启动动画并尽快返回句柄，不应在此等待整个动画结束。
    /// </summary>
    Task<AsyncDamageAnimationHandle> StartAnimationAsync(AsyncDamageSpawnContext context);

    /// <summary>
    /// 动画启动后立即由当前同步 Action 调用，不等待表现层 Impact。
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
