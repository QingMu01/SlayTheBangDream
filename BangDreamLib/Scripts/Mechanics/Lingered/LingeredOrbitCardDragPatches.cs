using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace BangDreamLib.Scripts.Mechanics.Lingered;

public sealed class LingeredOrbitCardDragPatches : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<BeginSubsideDragPatch>();
        patcher.RegisterPatch<CancelSubsideCardPlayPatch>();
        patcher.RegisterPatch<ClearSubsideDragPatch>();
        patcher.RegisterPatch<ExitSubsideHolderPatch>();
    }
}

internal sealed class BeginSubsideDragPatch : IPatchMethod
{
    public static string PatchId => "begin_subside_lingered_orbit_preview";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NHandCardHolder), nameof(NHandCardHolder.BeginDrag))];
    }

    public static void Postfix(NHandCardHolder __instance)
    {
        var card = __instance.CardModel;
        if (card is ISubsideCard)
        {
            card.Owner.AttachedData().LingeredOrbitManager.BeginSubsidePreview(card);
        }
    }
}

internal sealed class CancelSubsideCardPlayPatch : IPatchMethod
{
    public static string PatchId => "cancel_subside_card_play_lingered_orbit_preview";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCardPlay), "Cleanup")];
    }

    public static void Prefix(NCardPlay __instance, bool isFinished)
    {
        var card = __instance.Holder.CardModel;
        if (!isFinished && card is ISubsideCard)
        {
            card.Owner.AttachedData().LingeredOrbitManager.CancelSubsidePreview(card);
        }
    }
}

internal sealed class ClearSubsideDragPatch : IPatchMethod
{
    public static string PatchId => "clear_subside_lingered_orbit_preview";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NHandCardHolder), nameof(NHandCardHolder.Clear))];
    }

    public static void Prefix(NHandCardHolder __instance)
    {
        var card = __instance.CardNode?.Model;
        if (card is ISubsideCard)
        {
            card.Owner.AttachedData().LingeredOrbitManager.ScheduleSubsidePreviewCleanup(card);
        }
    }
}

internal sealed class ExitSubsideHolderPatch : IPatchMethod
{
    public static string PatchId => "exit_subside_holder_lingered_orbit_preview";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NHandCardHolder), nameof(NHandCardHolder._ExitTree))];
    }

    public static void Prefix(NHandCardHolder __instance)
    {
        var card = __instance.CardNode?.Model;
        if (card is ISubsideCard)
        {
            card.Owner.AttachedData().LingeredOrbitManager.ScheduleSubsidePreviewCleanup(card);
        }
    }
}
