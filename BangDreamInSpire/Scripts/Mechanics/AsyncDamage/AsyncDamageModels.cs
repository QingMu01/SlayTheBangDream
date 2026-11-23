using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Mechanics.AsyncDamage;

/// <summary>脱手动画句柄；玩法结算等待命中时刻，但始终由同步结算 Action 驱动。</summary>
public sealed class AsyncDamageAnimationHandle(
    Task landing,
    Task completion,
    VfxContext? context = null)
{
    /// <summary>命中信号驱动此任务完成，玩法结算等待它。</summary>
    public Task Landing { get; } = landing ?? throw new ArgumentNullException(nameof(landing));

    /// <summary>动画整体结束的时刻，仅用于观察生命周期与记录异常，不驱动玩法状态。</summary>
    public Task Completion { get; } = completion ?? throw new ArgumentNullException(nameof(completion));

    public VfxContext? Context { get; } = context;

    public static AsyncDamageAnimationHandle Immediate(VfxContext? context = null)
    {
        return new AsyncDamageAnimationHandle(Task.CompletedTask, Task.CompletedTask, context);
    }
}

/// <summary>脱手伤害批次；目标与伤害在发射时锁定，飞行期间不读取可变状态。</summary>
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
