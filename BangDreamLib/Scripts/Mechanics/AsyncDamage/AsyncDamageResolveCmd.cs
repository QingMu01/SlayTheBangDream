using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using STS2RitsuLib.Networking.ManagedActions;

namespace BangDreamLib.Scripts.Mechanics.AsyncDamage;

/// <summary>
/// 异步伤害的同步结算入口：命中时刻由发射者入队一条独立 Action，所有端按队列顺序在其内施加已锁定伤害。
/// 该 Action 视为机器发射（非玩家驱动），以便在回合结束阶段仍能执行，不与回合结束屏障互相死锁。
/// </summary>
public static class AsyncDamageResolveCmd
{
    private sealed record AsyncDamageResolvePayload(long BatchId, long NoteId);

    private static readonly RitsuLibManagedNetActionDescriptor<AsyncDamageResolvePayload> ResolveNetworkAction =
        new(
            "BangDreamLib",
            "async_damage_resolve",
            static payload => JsonSerializer.SerializeToUtf8Bytes(payload),
            static bytes => JsonSerializer.Deserialize<AsyncDamageResolvePayload>(bytes) ??
                            throw new InvalidOperationException("Invalid async damage resolve payload."),
            static context => ExecuteNetworkResolve(context),
            GameActionType.Combat);

    /// <summary>本结算动作的稳定操作码，供补丁识别；未注册时为 0。</summary>
    internal static ulong ResolveOpcode { get; private set; }

    /// <summary>在模组初始化阶段注册描述符，确保接收远端 Action 前本地已具备解码信息。</summary>
    internal static void InitializeNetwork()
    {
        ResolveOpcode = RitsuLibManagedNetActions.Register(ResolveNetworkAction);
    }

    /// <summary>命中时刻由发射者（本地拥有该角色的端）请求入队一条结算 Action。</summary>
    internal static void RequestResolve(long batchId, long noteId, Player? dealer)
    {
        var payload = new AsyncDamageResolvePayload(batchId, noteId);
        if (RitsuLibManagedNetActions.Request(null, ResolveNetworkAction, payload, dealer?.NetId))
            return;

        BangDreamLibCore.Logger.Warn(
            $"Async damage resolve was not accepted by the network layer (batch={batchId}, note={noteId}).");
    }

    /// <summary>该 Action 是否为异步伤害的结算动作（供 <c>IsGameActionPlayerDriven</c> 补丁使用）。</summary>
    internal static bool IsResolveAction(GameAction action)
    {
        return ResolveOpcode != 0 &&
               action is RitsuLibManagedGameAction managed &&
               managed.DescriptorOpcode == ResolveOpcode;
    }

    private static Task ExecuteNetworkResolve(RitsuLibManagedNetActionContext<AsyncDamageResolvePayload> context)
    {
        var combatState = context.Player.Creature.CombatState;
        if (combatState == null)
            return Task.CompletedTask;

        return CombatAsyncDamageManager.Shared.ResolveAsync(
            combatState, context.Message.BatchId, context.Message.NoteId);
    }
}
