using BangDreamLib.Scripts.Mechanics.ExtraDeck;
using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace BangDreamLib.Scripts.Nodes.Merchant;

/// <summary>
/// 小祥商人的音乐牌槽位：展示 <see cref="MusicCardMerchantEntry"/>，购买后卡片飞入额外卡组。
/// 场景为原版 merchant_card.tscn 的脚本覆盖实例，节点结构与 NMerchantCard 一致。
/// </summary>
public partial class NMusicCardSlot : NMerchantSlot
{
    private Node2D _saleVisual = null!;

    private Control _cardHolder = null!;

    private NCard? _cardNode;

    private MusicCardMerchantEntry _entry = null!;

    public override MerchantEntry Entry => _entry;

    protected override CanvasItem Visual => _cardHolder;

    public override void _Ready()
    {
        ConnectSignals();
        _cardHolder = GetNode<Control>("%CardHolder");
        _saleVisual = GetNode<Node2D>("%SaleVisual");
    }

    public void Fill(MusicCardMerchantEntry entry)
    {
        _entry = entry;
        entry.EntryUpdated += UpdateVisual;
        entry.PurchaseFailed += OnPurchaseFailed;
        entry.PurchaseCompleted += OnSuccessfulPurchase;
        UpdateVisual();
    }

    public void OnInventoryOpened()
    {
        if (_entry.CreationResult is not { HasBeenModified: true })
        {
            return;
        }

        TaskHelper.RunSafely(DoRelicFlash());
    }

    protected override void UpdateVisual()
    {
        if (_entry == null)
        {
            return;
        }

        base.UpdateVisual();
        var creationResult = _entry.CreationResult;
        if (creationResult == null)
        {
            Visible = false;
            MouseFilter = MouseFilterEnum.Ignore;
            ClearHoverTip();
            return;
        }

        if (_cardNode != null && _cardNode.Model != creationResult.Card)
        {
            _cardNode.QueueFreeSafely();
            _cardNode = null;
        }

        if (_cardNode == null)
        {
            _cardNode = NCard.Create(creationResult.Card);
            if (_cardNode == null)
            {
                return;
            }

            _cardHolder.AddChildSafely(_cardNode);
            _cardNode.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        }

        _costLabel.SetTextAutoSize(_entry.Cost.ToString());
        _saleVisual.Visible = false;
        _costLabel.Modulate = _entry.EnoughGold ? StsColors.cream : StsColors.red;
    }

    protected override async Task OnTryPurchase(MerchantInventory? inventory)
    {
        await _entry.OnTryPurchaseWrapper(inventory);
    }

    protected override void OnPreview()
    {
        ClearHoverTip();
        var card = _cardNode?.Model;
        if (card == null)
        {
            return;
        }

        var cards = new List<CardModel> { card };
        var inspectScreen = NGame.Instance?.GetInspectCardScreen();
        if (inspectScreen == null)
        {
            return;
        }

        inspectScreen.Open(cards, 0);
    }

    protected override void CreateHoverTip()
    {
        var card = _entry.CreationResult?.Card;
        if (card == null)
        {
            return;
        }

        NHoverTipSet.CreateAndShow(this, card.HoverTips)?
            .SetAlignment(_hitbox, HoverTip.GetHoverTipAlignment(this));
    }

    private void OnSuccessfulPurchase(PurchaseStatus _, MerchantEntry __)
    {
        TriggerMerchantHandToPointHere();
        SfxCmd.Play("event:/sfx/npcs/merchant/merchant_thank_yous");
        var cardNode = _cardNode;
        var card = cardNode?.Model;
        if (cardNode == null || card == null)
        {
            return;
        }

        _cardNode = null;
        NRun.Instance?.GlobalUi.ReparentCard(cardNode);
        NRun.Instance?.GlobalUi.TopBar.TrailContainer.AddChildSafely(NCardFlyVfx.Create(cardNode,
            BangDreamConst.ExtraDeck, isAddingToPile: true, card.Owner.Character.TrailPath));
        UpdateVisual();
    }

    private async Task DoRelicFlash()
    {
        var creationResult = _entry.CreationResult;
        if (creationResult == null)
        {
            return;
        }

        SceneTreeTimer source = GetTree().CreateTimer(0.4);
        await source.AwaitSignal(SceneTreeTimer.SignalName.Timeout, this);
        foreach (RelicModel modifyingRelic in creationResult.ModifyingRelics)
        {
            modifyingRelic.Flash();
            _cardNode?.FlashRelicOnCard(modifyingRelic);
        }
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        _cardNode?.QueueFreeSafely();
        if (_entry == null)
        {
            return;
        }

        _entry.EntryUpdated -= UpdateVisual;
        _entry.PurchaseFailed -= OnPurchaseFailed;
        _entry.PurchaseCompleted -= OnSuccessfulPurchase;
    }
}
