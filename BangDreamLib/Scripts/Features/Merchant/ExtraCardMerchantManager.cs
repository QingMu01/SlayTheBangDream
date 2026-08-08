using System.Reflection;
using System.Runtime.CompilerServices;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Utils;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Rooms;

namespace BangDreamLib.Scripts.Features.Merchant;

internal static class ExtraCardMerchantManager
{
    public const string InventoryScenePath =
        "res://BangDreamLib/scenes/merchant/extra_card_merchant_inventory.tscn";

    private const string MerchantButtonScenePath =
        "res://BangDreamLib/scenes/merchant/merchant_sakiko_button.tscn";

    private static readonly FieldInfo CharacterCardEntriesField =
        AccessTools.Field(typeof(MerchantInventory), "_characterCardEntries");

    private static readonly PropertyInfo CardRemovalEntryProperty =
        AccessTools.Property(typeof(MerchantInventory), nameof(MerchantInventory.CardRemovalEntry));

    private static readonly FieldInfo EntryCostField = AccessTools.Field(typeof(MerchantEntry), "_cost");
    private static readonly FieldInfo EntryPlayerField = AccessTools.Field(typeof(MerchantEntry), "_player");

    private static readonly ConditionalWeakTable<MerchantCardEntry, Marker> CardEntries = new();
    private static readonly ConditionalWeakTable<MerchantCardRemovalEntry, Marker> CardRemovalEntries = new();

    private static readonly AsyncLocal<int> CustomRemovalContextDepth = new();

    public static bool IsInCustomRemovalContext => CustomRemovalContextDepth.Value > 0;

    public static void AttachToRoom(NMerchantRoom room)
    {
        var player = room.Inventory.Inventory?.Player;
        if (player == null)
        {
            return;
        }

        var isMultiplayer = player.RunState.Players.Count > 1;
        var supportsExtraCardPool = player.Character is IExtraDeckSupportCharacter;
        if (!isMultiplayer && !supportsExtraCardPool)
        {
            return;
        }

        var inventories = new Dictionary<ulong, MerchantInventory>();
        foreach (var currentPlayer in player.RunState.Players)
        {
            if (currentPlayer.Character is IExtraDeckSupportCharacter)
            {
                inventories.Add(currentPlayer.NetId, CreateInventory(currentPlayer));
            }
        }

        var merchantScene = BangDreamPreloadManager.GetScene(MerchantButtonScenePath);
        if (merchantScene == null)
        {
            BangDreamLibCore.Logger.Error($"Unable to load extra card merchant scene: {MerchantButtonScenePath}");
            return;
        }

        var merchantButton = merchantScene.Instantiate<NMerchantButton>();
        merchantButton.Name = "ExtraCardMerchantButton";
        merchantButton.Position -= new Vector2(430f, 0f);
        merchantButton.IsLocalPlayerDead = supportsExtraCardPool && player.Creature.IsDead;
        merchantButton.PlayerDeadLines = MerchantRoom.Dialogue.PlayerDeadLines;
        room.GetNode<Control>("SceneContainer").AddChildSafely(merchantButton);

        NMerchantInventory? inventoryNode = null;
        if (supportsExtraCardPool && inventories.TryGetValue(player.NetId, out var localInventory))
        {
            inventoryNode = CreateInventoryNode(room, localInventory);
        }

        merchantButton.Connect(NMerchantButton.SignalName.MerchantOpened,
            Callable.From<NMerchantButton>(_ => OnMerchantOpened(room, merchantButton, inventoryNode, player)));
    }

    public static bool IsCustom(MerchantCardEntry entry)
    {
        return CardEntries.TryGetValue(entry, out _);
    }

    public static bool IsCustom(MerchantCardRemovalEntry entry)
    {
        return CardRemovalEntries.TryGetValue(entry, out _);
    }

    public static NMerchantInventory? GetOpenInventory(NMerchantRoom room)
    {
        var inventory = room.GetNodeOrNull<NMerchantInventory>("ExtraCardMerchantInventory");
        return inventory is { IsOpen: true } ? inventory : null;
    }

    public static void ApplyCardPriceMultiplier(MerchantCardEntry entry)
    {
        if (!IsCustom(entry))
        {
            return;
        }

        var cost = (int)EntryCostField.GetValue(entry)!;
        EntryCostField.SetValue(entry, Mathf.RoundToInt(cost * 1.2f));
    }

    public static void ApplyCardRemovalPrice(MerchantCardRemovalEntry entry)
    {
        if (!IsCustom(entry))
        {
            return;
        }

        var player = (Player)EntryPlayerField.GetValue(entry)!;
        var baseCost = AscensionHelper.GetValueIfAscension(AscensionLevel.Inflation, 100, 75);
        var removalsUsed = BangDreamConst.ExtraCardMerchant.Get(player).CardRemovalsUsed;
        EntryCostField.SetValue(entry, baseCost + MerchantCardRemovalEntry.PriceIncrease * removalsUsed);
    }

    public static bool BeginCustomRemovalContext(MerchantCardRemovalEntry entry)
    {
        if (!IsCustom(entry))
        {
            return false;
        }

        CustomRemovalContextDepth.Value++;
        return true;
    }

    public static void EndCustomRemovalContext(bool entered)
    {
        if (entered)
        {
            CustomRemovalContextDepth.Value--;
        }
    }

    public static async Task<bool> FinishCustomRemovalEntryAsync(MerchantCardRemovalEntry entry,
        Task<bool> purchaseTask)
    {
        var removed = await purchaseTask;
        if (removed)
        {
            entry.SetUsed();
            entry.OnMerchantInventoryUpdated();
        }

        return removed;
    }

    public static void RecordCustomRemoval(Player player)
    {
        BangDreamConst.ExtraCardMerchant.Modify(player, data => data.CardRemovalsUsed++);
    }

    private static NMerchantInventory CreateInventoryNode(NMerchantRoom room, MerchantInventory inventory)
    {
        var inventoryNode = BangDreamPreloadManager.GetScene(InventoryScenePath)
            .Instantiate<NMerchantInventory>();
        inventoryNode.Name = "ExtraCardMerchantInventory";
        inventoryNode.MouseFilter = Control.MouseFilterEnum.Ignore;
        room.AddChildSafely(inventoryNode);

        var constructionLabel = inventoryNode.GetNodeOrNull<Label>("%ConstructionLabel");
        if (constructionLabel != null)
        {
            constructionLabel.Text = new LocString("gameplay_ui",
                "BANG_DREAM_LIB_EXTRA_CARD_MERCHANT_CONSTRUCTION").GetFormattedText();
        }

        inventoryNode.Initialize(inventory, MerchantRoom.Dialogue);
        return inventoryNode;
    }

    private static MerchantInventory CreateInventory(Player player)
    {
        var inventory = new MerchantInventory(player);
        var entries = (List<MerchantCardEntry>)CharacterCardEntriesField.GetValue(inventory)!;
        var cardPool = ((IExtraDeckSupportCharacter)player.Character).ExtraCardPool
            .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .ToList();

        AddCardEntry(inventory, entries, cardPool, CardRarity.Uncommon);
        AddCardEntry(inventory, entries, cardPool, CardRarity.Uncommon);
        AddCardEntry(inventory, entries, cardPool, CardRarity.Uncommon);
        AddCardEntry(inventory, entries, cardPool, CardRarity.Rare);

        var removalEntry = new MerchantCardRemovalEntry(player);
        CardRemovalEntries.GetValue(removalEntry, static _ => new Marker());
        removalEntry.CalcCost();
        CardRemovalEntryProperty.SetValue(inventory, removalEntry);

        foreach (var entry in inventory.AllEntries)
        {
            entry.PurchaseCompleted += (_, _) =>
            {
                foreach (var currentEntry in inventory.AllEntries)
                {
                    currentEntry.OnMerchantInventoryUpdated();
                }
            };
        }

        return inventory;
    }

    private static void AddCardEntry(MerchantInventory inventory, List<MerchantCardEntry> entries,
        IReadOnlyCollection<CardModel> cardPool, CardRarity rarity)
    {
        var entry = new MerchantCardEntry(inventory.Player, inventory, cardPool, rarity);
        entries.Add(entry);
        CardEntries.GetValue(entry, static _ => new Marker());
        entry.Populate();
    }

    private static void OnMerchantOpened(NMerchantRoom room, NMerchantButton merchantButton,
        NMerchantInventory? inventory, Player player)
    {
        if (player.Character is not IExtraDeckSupportCharacter)
        {
            merchantButton.PlayDialogue(new LocString("gameplay_ui",
                "BANG_DREAM_LIB_EXTRA_CARD_MERCHANT_INVALID_TARGET"));
            return;
        }

        if (inventory is not { IsOpen: false })
        {
            return;
        }

        room.ProceedButton.Disable();
        room.MerchantButton.Disable();
        merchantButton.Disable();
        inventory.Open();
        inventory.Connect(NMerchantInventory.SignalName.InventoryClosed, Callable.From(() =>
        {
            room.MerchantButton.Enable();
            room.ProceedButton.Enable();
            room.ProceedButton.SetPulseState(isPulsing: true);
            merchantButton.Enable();
        }), (uint)GodotObject.ConnectFlags.OneShot);
    }

    private sealed class Marker;
}
