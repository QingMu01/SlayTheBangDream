using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace BangDreamLib.Scripts.Mechanics.AsyncDamage;

public class AsyncDamagePatches : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<WaitForAsyncDamageResolutionPatch>();
        patcher.RegisterPatch<AsyncResolveNotPlayerDrivenPatch>();
    }
}

/// <summary>
/// 回合结束前等待全部在飞异步伤害落地结算，避免音符伤害随回合切换被丢弃。
/// </summary>
internal class WaitForAsyncDamageResolutionPatch : IPatchMethod
{
    public static string PatchId => "wait_for_async_damage_before_ending_turn";

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(CombatManager), "WaitUntilQueueIsEmptyOrWaitingOnNonPlayerDrivenAction")
        ];
    }

    public static void Postfix(CombatManager __instance, ref Task __result)
    {
        var combatState = __instance.DebugOnlyGetState();
        if (combatState != null)
            __result = WaitForResolutionAsync(__result, combatState);
    }

    private static async Task WaitForResolutionAsync(Task originalQueueWait, ICombatState combatState)
    {
        await originalQueueWait;
        await CombatResolutionBarrier.WaitAsync(combatState);
    }
}

/// <summary>
/// 把异步伤害的结算动作视为非玩家驱动：它是机器在命中时刻发射的，需在回合结束阶段照常执行，
/// 否则会被 <c>StartCancellingAllPlayerDrivenCombatActions</c> 取消，令回合结束屏障与结算互相死锁。
/// </summary>
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
