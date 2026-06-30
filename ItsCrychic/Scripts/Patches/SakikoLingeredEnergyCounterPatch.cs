using BangDreamLib.Scripts.Extensions;
using ItsCrychic.Scripts.Nodes;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Patching.Models;

namespace ItsCrychic.Scripts.Patches;

public class SakikoLingeredEnergyCounterPatch : IPatchMethod
{
    public static string PatchId => "setup_sakiko_custom_energy_counter";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NEnergyCounter), nameof(NEnergyCounter._Ready))];
    }

    public static void Postfix(NEnergyCounter __instance, Player ____player)
    {
        var sakikoLingeredCounter = __instance.GetNodeOrNull<LingeredCounter>("%Layers");
        sakikoLingeredCounter?.SetContext(____player.AttachedData().LingeredEnergy);
    }
}