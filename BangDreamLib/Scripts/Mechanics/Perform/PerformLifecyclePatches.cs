using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Patching.Models;

namespace BangDreamLib.Scripts.Mechanics.Perform;

/// <summary>
/// 把歌单生命周期动作视为非玩家驱动：它在卡牌入堆时由机器发射，需在回合结束阶段照常执行，
/// 否则会被 <c>StartCancellingAllPlayerDrivenCombatActions</c> 取消，令卡牌留在歌单却永不分配槽位。
/// </summary>
internal class PerformLifecycleNotPlayerDrivenPatch : IPatchMethod
{
    public static string PatchId => "perform_lifecycle_not_player_driven";

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(ActionQueueSet), nameof(ActionQueueSet.IsGameActionPlayerDriven))
        ];
    }

    public static void Postfix(GameAction gameAction, ref bool __result)
    {
        if (__result && PerformManager.IsPerformLifecycleAction(gameAction))
            __result = false;
    }
}