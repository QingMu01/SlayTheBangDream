using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Features.AsyncDamage;

public enum AsyncDamageAnimationResult
{
    Triggered,
    CombatEnded
}

/// <summary>
/// 表示一个已经脱手运行的表现动画。Impact 仅描述表现层命中时刻，不能驱动玩法状态；
/// Completion 用于观察动画生命周期和记录异常。
/// </summary>
public sealed class AsyncDamageAnimationHandle(
    Task<AsyncDamageAnimationResult> impact,
    Task completion,
    VfxContext? context = null)
{
    public Task<AsyncDamageAnimationResult> Impact { get; } =
        impact ?? throw new ArgumentNullException(nameof(impact));

    public Task Completion { get; } = completion ?? throw new ArgumentNullException(nameof(completion));

    public VfxContext? Context { get; } = context;

    public static AsyncDamageAnimationHandle Immediate(VfxContext? context = null)
    {
        return new AsyncDamageAnimationHandle(
            Task.FromResult(AsyncDamageAnimationResult.Triggered),
            Task.CompletedTask,
            context);
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
    decimal ReservedDamage,
    bool IsChain,
    int ChainDepth
);

public sealed record AsyncDamageBatchContext(
    ICombatState CombatState,
    AsyncDamageBatchRequest Request,
    VfxContext? LastRootContext,
    int LaunchedCount
);
