using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Mechanics.MusicNote;

/// <summary>
/// 表示一个已经脱手运行的表现动画。Landing 是动画抵达目标的时刻，玩法结算等待它以保证伤害与命中同拍；
/// 动画本身不驱动玩法状态，结算始终发生在发起该批次的同步动作内。
/// </summary>
public sealed class AsyncDamageAnimationHandle(
    Task landing,
    Task completion,
    VfxContext? context = null)
{
    /// <summary>动画抵达目标的时刻。玩法结算等待此任务，使伤害在音符命中时才生效。</summary>
    public Task Landing { get; } = landing ?? throw new ArgumentNullException(nameof(landing));

    /// <summary>动画整体结束的时刻，仅用于观察生命周期与记录异常，不驱动玩法状态。</summary>
    public Task Completion { get; } = completion ?? throw new ArgumentNullException(nameof(completion));

    public VfxContext? Context { get; } = context;

    /// <summary>无表现时的退化句柄：立即命中，伤害即时结算。</summary>
    public static AsyncDamageAnimationHandle Immediate(VfxContext? context = null)
    {
        return new AsyncDamageAnimationHandle(Task.CompletedTask, Task.CompletedTask, context);
    }
}

/// <summary>
/// 一批共享来源、伤害实现和目标策略的脱手异步伤害动画。
/// </summary>
public sealed record AsyncDamageBatchRequest
{
    public required Creature Dealer { get; init; }
    public required int Count { get; init; }
    public required IAsyncDamageEffect Effect { get; init; }

    public Creature? InitialVisualSource { get; init; }
    public Creature? FixedTarget { get; init; }
    public AbstractModel? Source { get; init; }
    public int ChainCount { get; init; }
    public bool ExcludePreviousTargetFromChain { get; init; } = true;
}

public sealed record AsyncDamagePreparationContext(
    ICombatState CombatState,
    AsyncDamageBatchRequest Request,
    int Index,
    int Total
);

public sealed record AsyncDamageTargetContext(
    ICombatState CombatState,
    AsyncDamageBatchRequest Request,
    Creature Target
);

public sealed record AsyncDamageSpawnContext(
    ICombatState CombatState,
    AsyncDamageBatchRequest Request,
    Creature Target,
    Creature? VisualSource,
    long SequenceId,
    int Index,
    int Total,
    bool IsChain,
    int ChainDepth
);

public sealed record AsyncDamageHitContext(
    ICombatState CombatState,
    AsyncDamageBatchRequest Request,
    VfxContext? AnimationContext,
    Creature Target,
    long SequenceId,
    decimal LockedDamage,
    bool IsChain,
    int ChainDepth
);

public sealed record AsyncDamageBatchContext(
    ICombatState CombatState,
    AsyncDamageBatchRequest Request,
    VfxContext? LastRootContext,
    int LaunchedCount
);
