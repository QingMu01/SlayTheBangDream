using BangDreamLib.Scripts.Features;
using MegaCrit.Sts2.Core.Combat;
using STS2RitsuLib.Patching.Models;

namespace BangDreamLib.Scripts.Patches;

internal class WaitForCombatResolutionPatch : IPatchMethod
{
    public static string PatchId => "wait_for_async_combat_resolution_before_ending_turn";

    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(CombatManager), "WaitUntilQueueIsEmptyOrWaitingOnNonPlayerDrivenAction")];
    }

    public static void Postfix(CombatState ____state, ref Task __result)
    {
        __result = WaitForResolutionAsync(__result, ____state);
    }

    private static async Task WaitForResolutionAsync(Task originalQueueWait, ICombatState combatState)
    {
        await originalQueueWait;
        await CombatResolutionBarrier.WaitAsync(combatState);
    }
}