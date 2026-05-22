using BangDreamLib.Scripts.Interfaces.CardAugment;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Patches.Runtime;

public class AutoSetSubsideVarPatch
{
    public static void Postfix(ISubsideCardFlag __instance, ref IEnumerable<DynamicVar> __result)
    {
        if (__instance is CardModel)
        {
            var dynamicVars = __result.ToList();
            dynamicVars.Add(new IntVar("Subside", __instance.LingeredEnergyCost));
            __result = dynamicVars;
        }
    }
}