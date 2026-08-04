using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Features.AsyncDamage;

public enum AsyncDamageTargetPolicy
{
    AllowOverkill,
    AvoidReservedLethal,
    RetargetOnInvalid
}

public enum AsyncDamageAnimationResult
{
    Triggered,
    Cancelled,
    CombatEnded
}

/// <summary>
/// 表示一个已经脱手运行的动画。Impact 决定何时结算伤害，Completion 决定何时释放整批效果。
/// 两个任务可以来自 VFX 信号、Tween、计时器或任意异步动画流程。
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
    public AsyncDamageTargetPolicy TargetPolicy { get; init; } = AsyncDamageTargetPolicy.AvoidReservedLethal;
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
