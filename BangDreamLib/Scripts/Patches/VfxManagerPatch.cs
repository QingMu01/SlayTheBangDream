using BangDreamLib.Scripts.Nodes.VFX;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace BangDreamLib.Scripts.Patches;

public class VfxManagerPatches : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<InjectVfxManagerPatch>();
    }
}

internal class InjectVfxManagerPatch : IPatchMethod
{
    public static string PatchId => "on_combat_room_ready_inject_vfx_manager";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCombatRoom), nameof(NCombatRoom._Ready))];
    }

    public static void Postfix(NCombatRoom __instance)
    {
        var vfxContainer = __instance.CombatVfxContainer;
        var manager = new BangDreamVfxManager();
        manager.Name = "BangDreamVfxManager";
        vfxContainer.AddChildSafely(manager);
    }
}