using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Patching.Models;

namespace BangDreamInSpire.Scripts.Mechanics.Perform;

/// <summary>歌单生命周期动作必须标记为非玩家驱动，否则回合结束清理会取消它，使卡牌永不分配槽位。</summary>
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