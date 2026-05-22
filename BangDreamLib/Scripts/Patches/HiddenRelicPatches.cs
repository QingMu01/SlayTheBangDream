using System.Reflection.Emit;
using BangDreamLib.Scripts.Relics;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace BangDreamLib.Scripts.Patches;

public class HiddenRelicPatchSet : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<HiddenAdd>();
        patcher.RegisterPatch<HiddenInScreenView>();
        patcher.RegisterPatch<HiddenRelicInHistory>();
    }
}

internal class HiddenAdd : IPatchMethod
{
    public static string PatchId => "when_added_hidden_relic";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NRelicInventory), "Add")];
    }

    public static void Postfix(RelicModel relic, bool startsShown, int index,
        List<NRelicInventoryHolder> ____relicNodes)
    {
        foreach (var nRelicInventoryHolder in ____relicNodes.FindAll(holder => holder.Relic.Model is HiddenRelic))
        {
            nRelicInventoryHolder.Visible = false;
        }
    }
}

internal class HiddenInScreenView : IPatchMethod
{
    public static string PatchId => "when_open_view_screen_hidden_relic";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NRelicInventory), "OnRelicClicked")];
    }

    public static void Prefix(RelicModel model,
        List<NRelicInventoryHolder> ____relicNodes, ref List<NRelicInventoryHolder> __state)
    {
        __state = [..____relicNodes];
        ____relicNodes.RemoveAll(r => r.Relic.Model is HiddenRelic);
    }

    public static void Postfix(RelicModel model,
        ref List<NRelicInventoryHolder> ____relicNodes, List<NRelicInventoryHolder> __state)
    {
        ____relicNodes = __state;
    }
}

internal class HiddenRelicInHistory : IPatchMethod
{
    public static string PatchId => "when_open_history_screen_hidden_relic";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NRelicCollectionCategory), nameof(NRelicCollectionCategory.LoadRelics))];
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var index = codes.FindIndex(ci => ci.opcode == OpCodes.Stloc_1);
        if (index == -1) return codes;
        var insert = new[]
        {
            new CodeInstruction(OpCodes.Ldloc_1),
            new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(HiddenRelicInHistory),
                    nameof(FilterHiddenRelics))),
            new CodeInstruction(OpCodes.Stloc_1)
        };

        codes.InsertRange(index + 1, insert);
        return codes;
    }

    public static List<RelicModel> FilterHiddenRelics(List<RelicModel> relics)
    {
        relics.RemoveAll(r => r is HiddenRelic);
        return relics;
    }
}