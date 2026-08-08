using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Features.Merchant;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace BangDreamLib.Scripts.Patches;

public class ExtraCardMerchantPatches : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<AttachExtraCardMerchantToRoomPatch>();
        patcher.RegisterPatch<ExtraCardMerchantAnimationPatch>();
        patcher.RegisterPatch<ExtraCardMerchantFocusReticlePatch>();
        patcher.RegisterPatch<ExtraCardMerchantScreenContextPatch>();
        patcher.RegisterPatch<ExtraCardMerchantCardPricePatch>();
        patcher.RegisterPatch<ExtraCardMerchantRemovalPricePatch>();
        patcher.RegisterPatch<ExtraCardMerchantRemovalContextPatch>();
        patcher.RegisterPatch<EncodeExtraCardMerchantRemovalPatch>();
        patcher.RegisterPatch<TrackExtraCardMerchantRemovalPatch>();
        patcher.RegisterPatch<RedirectExtraCardMerchantRemovalUiPatch>();
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

        var inventory = ExtraCardMerchantManager.GetOpenInventory(room);
        if (inventory != null)
        {
            __result = inventory;
        }
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
        ExtraCardMerchantManager.AttachToRoom(__instance);
    }
}

internal class ExtraCardMerchantCardPricePatch : IPatchMethod
{
    public static string PatchId => "increase_extra_card_merchant_card_prices";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(MerchantCardEntry), nameof(MerchantCardEntry.CalcCost))];
    }

    public static void Postfix(MerchantCardEntry __instance)
    {
        ExtraCardMerchantManager.ApplyCardPriceMultiplier(__instance);
    }
}

internal class ExtraCardMerchantRemovalPricePatch : IPatchMethod
{
    public static string PatchId => "separate_extra_card_merchant_removal_price";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(MerchantCardRemovalEntry), nameof(MerchantCardRemovalEntry.CalcCost))];
    }

    public static void Postfix(MerchantCardRemovalEntry __instance)
    {
        ExtraCardMerchantManager.ApplyCardRemovalPrice(__instance);
    }
}

internal class ExtraCardMerchantRemovalContextPatch : IPatchMethod
{
    public static string PatchId => "mark_extra_card_merchant_removal_context";

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(MerchantCardRemovalEntry),
                nameof(MerchantCardRemovalEntry.OnTryPurchaseWrapper),
                [typeof(MerchantInventory), typeof(bool), typeof(bool)])
        ];
    }

    public static void Prefix(MerchantCardRemovalEntry __instance, out bool __state)
    {
        __state = ExtraCardMerchantManager.BeginCustomRemovalContext(__instance);
    }

    public static void Postfix(MerchantCardRemovalEntry __instance, bool __state, ref Task<bool> __result)
    {
        if (__state)
        {
            __result = ExtraCardMerchantManager.FinishCustomRemovalEntryAsync(__instance, __result);
        }

        ExtraCardMerchantManager.EndCustomRemovalContext(__state);
    }
}

internal class EncodeExtraCardMerchantRemovalPatch : IPatchMethod
{
    private const int CustomRemovalFlag = 1 << 30;

    public static string PatchId => "encode_extra_card_merchant_removal_message";

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(OneOffSynchronizer), nameof(OneOffSynchronizer.DoLocalMerchantCardRemoval),
                [typeof(int), typeof(bool)])
        ];
    }

    public static void Prefix(ref int goldCost)
    {
        if (ExtraCardMerchantManager.IsInCustomRemovalContext)
        {
            goldCost |= CustomRemovalFlag;
        }
    }
}

internal class RedirectExtraCardMerchantRemovalUiPatch : IPatchMethod
{
    public static string PatchId => "redirect_extra_card_merchant_removal_ui";

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(NMerchantInventory),
                nameof(NMerchantInventory.OnCardRemovalUsed))
        ];
    }

    public static bool Prefix()
    {
        return !ExtraCardMerchantManager.IsInCustomRemovalContext;
    }
}

internal class TrackExtraCardMerchantRemovalPatch : IPatchMethod
{
    private const int CustomRemovalFlag = 1 << 30;

    public static string PatchId => "track_extra_card_merchant_removal_separately";

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(OneOffSynchronizer), "DoMerchantCardRemoval",
                [typeof(Player), typeof(int), typeof(bool)])
        ];
    }

    public static bool Prefix(Player player, ref int goldCost, bool cancelable, out bool __state,
        ref Task<bool> __result)
    {
        __state = (goldCost & CustomRemovalFlag) != 0;
        if (__state)
        {
            goldCost &= ~CustomRemovalFlag;
            __result = DoCustomMerchantCardRemoval(player, goldCost, cancelable);
            return false;
        }

        return true;
    }

    public static void Postfix(Player player, bool __state, ref Task<bool> __result)
    {
        if (__state)
        {
            __result = TrackAsync(player, __result);
        }
    }

    private static async Task<bool> TrackAsync(Player player, Task<bool> removalTask)
    {
        var removed = await removalTask;
        if (removed)
        {
            ExtraCardMerchantManager.RecordCustomRemoval(player);
        }

        return removed;
    }

    private static async Task<bool> DoCustomMerchantCardRemoval(Player player, int goldCost, bool cancelable)
    {
        var prefs = new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1)
        {
            Cancelable = cancelable,
            RequireManualConfirmation = true
        };
        var card = (await ExtraPileCmd.FromExtraDeckForRemoval(player, prefs)).FirstOrDefault();
        if (card == null)
        {
            return false;
        }

        await PlayerCmd.LoseGold(goldCost, player, GoldLossType.Spent);
        await ExtraPileCmd.RemoveFromExtraDeck(card);
        return true;
    }
}
