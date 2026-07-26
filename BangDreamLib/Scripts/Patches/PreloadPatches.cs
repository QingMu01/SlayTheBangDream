using BangDreamLib.Scripts.Utils;
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
        patcher.RegisterPatch<PrepareCombatAssetsPatch>();
        patcher.RegisterPatch<PreloadCombatPatch>();
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
            new ModPatchTarget(typeof(PreloadManager), nameof(PreloadManager.LoadCommonAndMainMenuAssets))
        ];
    }

    public static void Postfix(ref Task __result)
    {
        __result = LoadCommonAssetsAfter(__result);
    }

    private static async Task LoadCommonAssetsAfter(Task originalPreload)
    {
        await originalPreload;
        await BangDreamPreloadManager.LoadCommonAssets();
    }
}

internal class PrepareCombatAssetsPatch : IPatchMethod
{
    public static string PatchId => "prepare_mod_combat_assets_for_preload_manager";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(NGame), "StartRun"),
            new ModPatchTarget(typeof(NGame), "LoadRun")
        ];
    }

    public static void Prefix(RunState runState)
    {
        BangDreamPreloadManager.PrepareCombatAssets(runState.Players);
    }
}

internal class PreloadCombatPatch : IPatchMethod
{
    public static string PatchId => "add_mod_combat_assets_to_preload_manager";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(PreloadManager), nameof(PreloadManager.LoadRunAssets))
        ];
    }

    public static void Postfix(ref Task __result)
    {
        __result = LoadCombatAssetsAfter(__result);
    }

    private static async Task LoadCombatAssetsAfter(Task originalPreload)
    {
        await originalPreload;
        await BangDreamPreloadManager.LoadPreparedCombatAssets();
    }
}
