using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Nodes.Merchant;
using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Rooms;

namespace BangDreamInSpire.Scripts.Mechanics.ExtraDeck;

/// <summary>
/// 额外卡组商人管理器（小祥商人）：为商人房间附加商人按钮与自定义库存。
/// 第一区域售卖额外卡池音乐牌（入额外卡组），第二区域售卖无色牌（入主卡组），
/// 第三区域为附魔/贴纸施工占位，第四区域提供额外卡组删牌服务。
/// 未实现额外卡池的角色打开时，第一、四区域显示未开放。
/// </summary>
internal static class ExtraCardMerchantManager
{
    public const string InventoryScenePath =
        "res://BangDreamInSpire/scenes/merchant/extra_card_merchant_inventory.tscn";

    private const string MerchantButtonScenePath =
        "res://BangDreamInSpire/scenes/merchant/merchant_sakiko_button.tscn";

    private const string NotOpenLocKey = "BANG_DREAM_IN_SPIRE_EXTRA_CARD_MERCHANT_NOT_OPEN";

    public static void AttachToRoom(NMerchantRoom room)
    {
        var player = room.Inventory.Inventory?.Player;
        if (player == null)
        {
            return;
        }

        var isMultiplayer = player.RunState.Players.Count > 1;
        var supportsExtraCardPool = BangDreamCapabilities.HasExtraDeck(player);
        if (!isMultiplayer && !supportsExtraCardPool)
        {
            return;
        }

        var merchantButton = BangDreamPreloadManager.GetScene(MerchantButtonScenePath).Instantiate<NMerchantButton>();
        merchantButton.Name = "ExtraCardMerchantButton";
        merchantButton.Position -= new Vector2(430f, 100f);
        merchantButton.IsLocalPlayerDead = supportsExtraCardPool && player.Creature.IsDead;
        merchantButton.PlayerDeadLines = MerchantRoom.Dialogue.PlayerDeadLines;
        room.GetNode<Control>("SceneContainer").AddChildSafely(merchantButton);

        var binding = CreateBinding(inventoryNode: CreateInventoryNode(room), player, supportsExtraCardPool);
        merchantButton.Connect(NMerchantButton.SignalName.MerchantOpened,
            Callable.From<NMerchantButton>(_ => OnMerchantOpened(room, merchantButton, binding)));
    }

    public static NMerchantInventory? GetOpenInventory(NMerchantRoom room)
    {
        var inventory = room.GetNodeOrNull<NMerchantInventory>("ExtraCardMerchantInventory");
        return inventory is { IsOpen: true } ? inventory : null;
    }

    private static NMerchantInventory CreateInventoryNode(NMerchantRoom room)
    {
        var inventoryNode = BangDreamPreloadManager.GetScene(InventoryScenePath).Instantiate<NMerchantInventory>();
        inventoryNode.Name = "ExtraCardMerchantInventory";
        inventoryNode.MouseFilter = Control.MouseFilterEnum.Ignore;
        room.AddChildSafely(inventoryNode);
        return inventoryNode;
    }

    private static MerchantBinding CreateBinding(NMerchantInventory inventoryNode, Player player,
        bool supportsExtraCardPool)
    {
        var notOpenText = new LocString("gameplay_ui", NotOpenLocKey).GetFormattedText();
        inventoryNode.GetNode<Label>("%ConstructionLabel").Text =
            new LocString("gameplay_ui", "BANG_DREAM_IN_SPIRE_EXTRA_CARD_MERCHANT_CONSTRUCTION").GetFormattedText();
        inventoryNode.GetNode<Label>("%NotOpenZone1Label").Text = notOpenText;
        inventoryNode.GetNode<Label>("%NotOpenZone4Label").Text = notOpenText;

        // 0.113 起 MerchantInventory 提供公开的全参构造，模组可以直接持有自己的无色牌条目列表，
        // 不必再反射写入私有的 _colorlessCardEntries。构造函数保存的是列表引用，
        // 因此先构造库存、再逐条 Populate 追加，与原先的时序等价。
        var colorlessEntries = new List<MerchantCardEntry>();
        var inventory = new MerchantInventory(
            player,
            characterCardEntries: [],
            colorlessCardEntries: colorlessEntries,
            relicEntries: [],
            potionEntries: [],
            cardRemovalEntry: null,
            blitzEntries: []);
        PopulateColorlessEntries(inventory, colorlessEntries, player);
        var musicEntries = new List<MusicCardMerchantEntry>();
        ExtraDeckRemovalMerchantEntry? removalEntry = null;
        if (supportsExtraCardPool)
        {
            var duplicateGuard = new HashSet<CardModel>();
            var cardPool = ((IExtraDeckSupportCharacter)player.Character).ExtraCardPool;
            foreach (var rarity in new[] { CardRarity.Uncommon, CardRarity.Uncommon, CardRarity.Rare })
            {
                var entry = new MusicCardMerchantEntry(player, cardPool, rarity, duplicateGuard);
                entry.Populate();
                musicEntries.Add(entry);
            }

            removalEntry = new ExtraDeckRemovalMerchantEntry(player);
        }

        inventoryNode.Initialize(inventory, MerchantRoom.Dialogue);
        // 音乐牌与删牌条目刻意不进 inventory.AllEntries（库存列表只收原版条目类型），
        // 靠 BindSlots 手工绑到场景槽位；因此下面的购买刷新要单独遍历这两组条目。
        BindSlots(inventoryNode, musicEntries, removalEntry, supportsExtraCardPool);

        void OnAnyPurchaseCompleted(PurchaseStatus _, MerchantEntry __)
        {
            foreach (var entry in inventory.AllEntries)
            {
                entry.OnMerchantInventoryUpdated();
            }

            foreach (var entry in musicEntries)
            {
                entry.OnMerchantInventoryUpdated();
            }

            removalEntry?.OnMerchantInventoryUpdated();
            UpdateFocusNavigation(inventoryNode);
        }

        foreach (var entry in colorlessEntries.Concat<MerchantEntry>(musicEntries))
        {
            entry.PurchaseCompleted += OnAnyPurchaseCompleted;
        }

        if (removalEntry != null)
        {
            removalEntry.PurchaseCompleted += OnAnyPurchaseCompleted;
        }

        return new MerchantBinding(inventoryNode, removalEntry != null);
    }

    private static void PopulateColorlessEntries(MerchantInventory inventory, List<MerchantCardEntry> entries,
        Player player)
    {
        var colorlessPool = ModelDb.CardPool<ColorlessCardPool>()
            .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .ToList();
        foreach (var rarity in new[] { CardRarity.Uncommon, CardRarity.Rare })
        {
            var entry = new MerchantCardEntry(player, colorlessPool, rarity);
            entry.Populate(inventory);
            entries.Add(entry);
        }
    }

    private static void BindSlots(NMerchantInventory inventoryNode, List<MusicCardMerchantEntry> musicEntries,
        ExtraDeckRemovalMerchantEntry? removalEntry, bool supportsExtraCardPool)
    {
        var musicSlots = inventoryNode.GetNode<Control>("%MusicCards").GetChildren().OfType<NMusicCardSlot>().ToList();
        var removalSlot = inventoryNode.GetNodeOrNull<NExtraDeckRemovalSlot>("%ExtraDeckRemoval");
        if (supportsExtraCardPool)
        {
            // 只有真的会 Fill 的槽位才 Initialize：基类 Initialize 会订阅 Player.GoldChanged
            // 并连接悬停信号，而「Initialize 了但从未 Fill」的槽位在 UpdateVisual 里没有条目可读。
            foreach (var musicSlot in musicSlots)
            {
                musicSlot.Initialize(inventoryNode);
            }

            if (removalSlot != null)
            {
                removalSlot.Initialize(inventoryNode);
            }

            for (var i = 0; i < musicSlots.Count && i < musicEntries.Count; i++)
            {
                musicSlots[i].Fill(musicEntries[i]);
            }

            removalSlot?.Fill(removalEntry!);
        }
        else
        {
            inventoryNode.GetNode<Control>("%MusicCards").Visible = false;
            if (removalSlot != null)
            {
                removalSlot.Visible = false;
            }

            inventoryNode.GetNode<Control>("%NotOpenZone1").Visible = true;
            inventoryNode.GetNode<Control>("%NotOpenZone4").Visible = true;
        }
    }

    private static void UpdateFocusNavigation(NMerchantInventory inventoryNode)
    {
        var musicSlots = inventoryNode.GetNode<Control>("%MusicCards").GetChildren()
            .OfType<NMerchantSlot>().Where(s => s.Visible).ToList();
        var colorlessSlots = inventoryNode.GetNode<Control>("%ColorlessCards").GetChildren()
            .OfType<NMerchantSlot>().Where(s => s.Visible).ToList();
        var removalSlot = inventoryNode.GetNodeOrNull<NExtraDeckRemovalSlot>("%ExtraDeckRemoval");
        var firstRow = new List<NMerchantSlot>(musicSlots);
        firstRow.AddRange(colorlessSlots);
        var removal = removalSlot is { Visible: true } ? new List<NMerchantSlot> { removalSlot } : [];

        WireHorizontalRow(firstRow);

        foreach (var slot in firstRow)
        {
            slot.FocusNeighborTop = slot.GetPath();
            slot.FocusNeighborBottom = slot.GetPath();
        }

        if (removal.Count > 0 && firstRow.Count > 0)
        {
            var rowEnd = firstRow[^1];
            rowEnd.FocusNeighborBottom = removal[0].GetPath();
            removal[0].FocusNeighborTop = rowEnd.GetPath();
            removal[0].FocusNeighborBottom = removal[0].GetPath();
            removal[0].FocusNeighborLeft = removal[0].GetPath();
            removal[0].FocusNeighborRight = removal[0].GetPath();
        }
    }

    private static void WireHorizontalRow(IReadOnlyList<NMerchantSlot> row)
    {
        for (var i = 0; i < row.Count; i++)
        {
            row[i].FocusNeighborLeft = (i > 0 ? row[i - 1] : row[i]).GetPath();
            row[i].FocusNeighborRight = (i < row.Count - 1 ? row[i + 1] : row[i]).GetPath();
        }
    }

    private static void OnMerchantOpened(NMerchantRoom room, NMerchantButton merchantButton, MerchantBinding binding)
    {
        var inventoryNode = binding.InventoryNode;
        if (inventoryNode.IsOpen)
        {
            return;
        }

        room.ProceedButton.Disable();
        room.MerchantButton.Disable();
        merchantButton.Disable();
        inventoryNode.Open();

        if (binding.SupportsExtraCardPool)
        {
            foreach (var musicSlot in inventoryNode.GetNode<Control>("%MusicCards").GetChildren().OfType<NMusicCardSlot>())
            {
                musicSlot.OnInventoryOpened();
            }
        }

        inventoryNode.Connect(NMerchantInventory.SignalName.InventoryClosed, Callable.From(() =>
        {
            room.MerchantButton.Enable();
            room.ProceedButton.Enable();
            room.ProceedButton.SetPulseState(isPulsing: true);
            merchantButton.Enable();
        }), (uint)GodotObject.ConnectFlags.OneShot);
    }

    private sealed record MerchantBinding(
        NMerchantInventory InventoryNode,
        bool SupportsExtraCardPool);
}
