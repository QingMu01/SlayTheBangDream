using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace BangDreamLib.Scripts.Mechanics.ExtraDeck;

public class ExtraCardMerchantPatches : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<AttachExtraCardMerchantToRoomPatch>();
        patcher.RegisterPatch<ExtraCardMerchantAnimationPatch>();
        patcher.RegisterPatch<ExtraCardMerchantFocusReticlePatch>();
        patcher.RegisterPatch<ExtraCardMerchantScreenContextPatch>();
    }
}

internal class AttachExtraCardMerchantToRoomPatch : IPatchMethod
{
    public static string PatchId => "attach_extra_card_merchant_to_shop_room";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NMerchantRoom), nameof(NMerchantRoom._Ready))];
    }

    public static void Postfix(NMerchantRoom __instance)
    {
        SakikoMerchantManager.AttachToRoom(__instance);
    }
}

internal class ExtraCardMerchantAnimationPatch : IPatchMethod
{
    internal const string MerchantButtonScenePath =
        "res://BangDreamLib/scenes/merchant/merchant_sakiko_button.tscn";

    public static string PatchId => "replace_extra_card_merchant_idle_animation";

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(SpineNodeExtensions), nameof(SpineNodeExtensions.RunWhenSpineReady),
                [typeof(Node), typeof(MegaSprite), typeof(Action<MegaAnimationState>)])
        ];
    }

    public static void Prefix(Node host, MegaSprite sprite, ref Action<MegaAnimationState> onReady)
    {
        if (host is not NMerchantButton { SceneFilePath: MerchantButtonScenePath } merchantButton)
        {
            return;
        }

        var merchantVisual = merchantButton.GetNodeOrNull("MerchantVisual");
        if (merchantVisual == null || sprite.BoundObject.GetInstanceId() != merchantVisual.GetInstanceId())
        {
            return;
        }

        onReady = animationState => animationState.SetAnimation("idle");
    }
}

internal class ExtraCardMerchantFocusReticlePatch : IPatchMethod
{
    public static string PatchId => "show_extra_card_merchant_focus_reticle";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NMerchantButton), "OnFocus", Type.EmptyTypes)];
    }

    public static void Postfix(NMerchantButton __instance)
    {
        if (__instance.SceneFilePath != ExtraCardMerchantAnimationPatch.MerchantButtonScenePath)
        {
            return;
        }

        var reticle = __instance.GetNodeOrNull<NSelectionReticle>("%MerchantSelectionReticle");
        if (reticle is { IsSelected: false })
        {
            reticle.OnSelect();
        }
    }
}

internal class ExtraCardMerchantScreenContextPatch : IPatchMethod
{
    public static string PatchId => "recognize_extra_card_merchant_as_active_screen";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(ActiveScreenContext), nameof(ActiveScreenContext.GetCurrentScreen))];
    }

    public static void Postfix(ref IScreenContext? __result)
    {
        if (__result is not NMerchantRoom room)
        {
            return;
        }

        var inventory = SakikoMerchantManager.GetOpenInventory(room);
        if (inventory != null)
        {
            __result = inventory;
        }
    }
}
