using BangDreamLib.Scripts.Multiplayer;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace BangDreamLib.Scripts.Patches;

public class AttachePlayerExtraContentPatches : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<OnStartedRunInitPatch>();
        patcher.RegisterPatch<OnEndedRunClearPatch>();
    }
}

internal class OnStartedRunInitPatch : IPatchMethod
{
    public static string PatchId => "on_started_run_set_player_data";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(RunManager), nameof(RunManager.Launch))];
    }

    public static void Postfix(RunState __result)
    {
        BangDreamTools.RunState = __result;
        foreach (var player in __result.Players)
        {
            if (LocalContext.IsMe(player))
            {
                BangDreamTools.LocalPlayer = player;
            }
            else
            {
                AttachePlayerData.State.GetOrCreate(player);
            }
        }

        if (BangDreamTools.LocalPlayer == null)
        {
            BangDreamLibCore.Logger.Warn("Run start but not found local player");
        }
    }
}

internal class OnEndedRunClearPatch : IPatchMethod
{
    public static string PatchId => "on_ended_run_clear_local_player_data_cache";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(RunManager), nameof(RunManager.CleanUp))];
    }

    public static void Finalizer()
    {
        BangDreamTools.RunState = null;
        BangDreamTools.LocalPlayer = null;
        BangDreamLibCore.Logger.Info("Run end clear the local player and data.");
    }
}