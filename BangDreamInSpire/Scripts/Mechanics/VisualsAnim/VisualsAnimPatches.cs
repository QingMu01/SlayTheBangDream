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

/// <summary>
/// 演出期间把 <see cref="NCreature.VfxSpawnPosition" /> 重定向到视觉代理。
/// <para>
/// 本体有几十个 VFX 类都读这个属性（<c>NStabVfx</c>、<c>NLineBurstVfx</c>、<c>NHealNumVfx</c> ……），
/// 模组侧同样（<c>MusicNoteAsyncDamageEffect</c>、<c>PerformMechanic</c>、多个音符/破绽特效）。
/// 它是「命中特效落在哪儿」的唯一收口，所以只补这一处就能让所有特效跟随演出中的角色。
/// </para>
/// <para>
/// 这是 STS1 <c>EnhanceCreatureRenderPatch.Info.renderer</c> 那一层间接的同构移植：那边也是给每个生物挂一个
/// renderer 注册表，把渲染与坐标全部重定向过去。
/// </para>
/// </summary>
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

/// <summary>
/// 把本体的受击抖动转发到克隆。
/// <para>
/// <see cref="NCreature.AnimShake" /> 写的是 <c>Visuals.Position</c>，演出期间那是隐藏中的原体，抖动看不见。
/// 克隆位于独立的移动载体之下，所以抖动写它自己的局部位置，不会与位移互相覆盖。
/// </para>
/// </summary>
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
