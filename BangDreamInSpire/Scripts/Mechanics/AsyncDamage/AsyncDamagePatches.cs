using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace BangDreamInSpire.Scripts.Mechanics.AsyncDamage;

public class AsyncDamagePatches : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<WaitForAsyncDamageResolutionPatch>();
        patcher.RegisterPatch<AsyncResolveNotPlayerDrivenPatch>();
    }
}

/// <summary>玩家结束回合前等待在飞伤害；不扩大其他转场的同步等待范围。</summary>
internal class WaitForAsyncDamageResolutionPatch : IPatchMethod
{
    public static string PatchId => "wait_for_async_damage_before_ending_turn";

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            // 唯一等待点为玩家结束回合。
            new ModPatchTarget(typeof(CombatManager), "WaitUntilQueueIsEmptyOrWaitingOnNonPlayerDrivenAction")
        ];
    }

    public static void Postfix(CombatManager __instance, ref Task __result)
    {
        var combatState = __instance.DebugOnlyGetState();
        if (combatState != null)
            __result = WaitForResolutionAsync(__result, combatState);
    }

    /// <summary>屏障等待上限，超时强制放行并记录。</summary>
    private const int ResolutionTimeoutMs = 5000;

    private static async Task WaitForResolutionAsync(Task originalQueueWait, ICombatState combatState)
    {
        await originalQueueWait;

        try
        {
            await CombatResolutionBarrier
                .WaitAsync(combatState)
                .WaitAsync(TimeSpan.FromMilliseconds(ResolutionTimeoutMs));
        }
        catch (TimeoutException)
        {
            BangDreamInSpireCore.Logger.Error(
                $"Async damage resolution did not finish within {ResolutionTimeoutMs}ms; " +
                "forcing the barrier open so the turn can end.");
            CombatResolutionBarrier.ForceComplete(combatState);
        }
    }
}

/// <summary>结算动作按非玩家驱动处理，避免回合结束时取消。</summary>
internal class AsyncResolveNotPlayerDrivenPatch : IPatchMethod
{
    public static string PatchId => "async_damage_resolve_not_player_driven";

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(ActionQueueSet), nameof(ActionQueueSet.IsGameActionPlayerDriven))
        ];
    }

    public static void Postfix(GameAction gameAction, ref bool __result)
    {
        if (__result && AsyncDamageResolveCmd.IsResolveAction(gameAction))
            __result = false;
    }
}
