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
        patcher.RegisterPatch<InjectModAssetSetsPatch>();
        patcher.RegisterPatch<PrepareCombatAssetsPatch>();
    }
}

internal class InjectModAssetSetsPatch : IPatchMethod
{
    public static string PatchId => "inject_mod_assets_into_vanilla_asset_sets";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(PreloadManager), "LoadAssetSets", [typeof(string), typeof(IEnumerable<string>[])])
        ];
    }

    public static void Prefix(string name, ref IEnumerable<string>[] assetSets)
    {
        var modAssets = BangDreamPreloadManager.GetAssetsForSession(name);
        if (modAssets.Count == 0)
        {
            return;
        }

        assetSets = [.. assetSets, modAssets];
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