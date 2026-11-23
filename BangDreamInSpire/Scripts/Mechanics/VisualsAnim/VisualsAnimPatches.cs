using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace BangDreamInSpire.Scripts.Mechanics.VisualsAnim;

public class VisualsAnimPatches : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<VisualsAnimVfxSpawnPositionPatch>();
        patcher.RegisterPatch<VisualsAnimShakeForwardPatch>();
    }
}

/// <summary>演出期间将 VfxSpawnPosition 重定向到视觉代理，统一命中特效落点。</summary>
internal class VisualsAnimVfxSpawnPositionPatch : IPatchMethod
{
    public static string PatchId => "visuals_anim_vfx_spawn_position_redirect";

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(NCreature), nameof(NCreature.VfxSpawnPosition), MethodType.Getter)
        ];
    }

    public static void Postfix(NCreature __instance, ref Vector2 __result)
    {
        if (VisualsAnimProxyRegistry.TryGetActive(__instance.Entity, out var proxy))
            __result = proxy.VfxSpawnPosition;
    }
}

/// <summary>将原体受击抖动转发到克隆局部坐标，避免与位移争写。</summary>
internal class VisualsAnimShakeForwardPatch : IPatchMethod
{
    public static string PatchId => "visuals_anim_forward_anim_shake";

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(NCreature), nameof(NCreature.AnimShake))
        ];
    }

    public static void Postfix(NCreature __instance)
    {
        if (VisualsAnimProxyRegistry.TryGetActive(__instance.Entity, out var proxy))
            proxy.PlayShake();
    }
}
