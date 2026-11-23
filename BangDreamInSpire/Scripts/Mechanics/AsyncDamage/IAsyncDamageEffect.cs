namespace BangDreamInSpire.Scripts.Mechanics.AsyncDamage;

/// <summary>定义脱手动画及其命中结算；实现只需提供命中与结束任务，无需继承指定 VFX 类型。</summary>
public interface IAsyncDamageEffect
{
    /// <summary>在根动画启动前播放施法动作或控制生成间隔；运行于表现泵，不占用同步 Action。</summary>
    Task BeforeSpawnAsync(AsyncDamagePreparationContext context)
    {
        return Task.CompletedTask;
    }

    /// <summary>伤害在发射时锁定并用于目标预留和命中结算，避免飞行期间的状态变化破坏多端一致性。</summary>
    decimal GetLockedDamage(AsyncDamageTargetContext context);

    /// <summary>启动动画后立即返回；<see cref="AsyncDamageAnimationHandle.Landing" /> 必须在抵达目标时完成。</summary>
    Task<AsyncDamageAnimationHandle> StartAnimationAsync(AsyncDamageSpawnContext context);

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
