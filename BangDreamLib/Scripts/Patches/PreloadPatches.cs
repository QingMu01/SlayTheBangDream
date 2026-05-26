using BangDreamLib.Scripts.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace BangDreamLib.Scripts.Patches;

public class PreloadPatches : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<PreloadCommonPatch>();
        patcher.RegisterPatch<PreloadRunPatch>();
    }
}

internal class PreloadCommonPatch : IPatchMethod
{
    public static string PatchId => "add_mod_extra_common_asset_to_preload_manager";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(PreloadManager), nameof(PreloadManager.LoadCommonAndMainMenuAssets),
                MethodType.Async)
        ];
    }

    public static void Postfix()
    {
        _ = BangDreamPreloadManager.LoadCommonAssets();
    }
}

internal class PreloadRunPatch : IPatchMethod
{
    public static string PatchId => "add_mod_extra_run_asset_to_preload_manager";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(NGame), "StartRun"),
            new ModPatchTarget(typeof(NGame), "LoadRun")
        ];
    }

    public static void Postfix(RunState runState)
    {
        _ = BangDreamPreloadManager.LoadRunAssets(runState.Players);
    }
}