using BangDreamLib.Scripts.Mechanics.ExtraDeck;
using Godot;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace BangDreamLib.Scripts.Nodes.Merchant;

/// <summary>
/// 小祥商人的删牌服务槽位：展示 <see cref="ExtraDeckRemovalMerchantEntry"/>，使用后播放原版已用动画。
/// 场景为原版 merchant_card_removal.tscn 的脚本覆盖实例，节点结构与 NMerchantCardRemoval 一致。
/// </summary>
public partial class NExtraDeckRemovalSlot : NMerchantSlot
{
    private Sprite2D _removalVisual = null!;

    private AnimationPlayer _animator = null!;

    private Control _costContainer = null!;

    private bool _isUnavailable;

    private ExtraDeckRemovalMerchantEntry _entry = null!;

    private static LocString Title =>
        new("gameplay_ui", "BANG_DREAM_LIB_EXTRA_CARD_MERCHANT_REMOVAL_TITLE");

    private static LocString Description =>
        new("gameplay_ui", "BANG_DREAM_LIB_EXTRA_CARD_MERCHANT_REMOVAL_DESCRIPTION");

    public override MerchantEntry Entry => _entry;

    protected override CanvasItem Visual => _removalVisual;

    public override void _Ready()
    {
        ConnectSignals();
        _removalVisual = GetNode<Sprite2D>("%Visual");
        _animator = GetNode<AnimationPlayer>("%Animation");
        _costContainer = GetNode<Control>("Cost");
    }

    public void Fill(ExtraDeckRemovalMerchantEntry entry)
    {
        _entry = entry;
        entry.EntryUpdated += UpdateVisual;
        entry.PurchaseFailed += OnPurchaseFailed;
        entry.PurchaseCompleted += OnSuccessfulPurchase;
        if (!Hook.ShouldAllowMerchantCardRemoval(Player!.RunState, Player))
        {
            _entry.SetUsed();
        }

        UpdateVisual();
    }

    protected override void UpdateVisual()
    {
        base.UpdateVisual();
        if (_isUnavailable)
        {
            return;
        }

        if (_entry.Used)
        {
            _hitbox.MouseFilter = MouseFilterEnum.Ignore;
            _animator.CurrentAnimation = "Used";
            _isUnavailable = true;
            _animator.Play();
            _costLabel.Visible = false;
            _costContainer.Visible = false;
            FocusMode = FocusModeEnum.None;
        }
        else
        {
            MouseFilter = MouseFilterEnum.Stop;
            _costLabel.Visible = true;
            _costLabel.SetTextAutoSize(_entry.Cost.ToString());
            _costLabel.Modulate = _entry.EnoughGold ? StsColors.cream : StsColors.red;
            _costContainer.Visible = true;
            FocusMode = FocusModeEnum.All;
        }

        ClearHoverTip();
    }

    protected override async Task OnTryPurchase(MerchantInventory? inventory)
    {
        await _entry.OnTryPurchaseWrapper(inventory);
    }

    protected override void CreateHoverTip()
    {
        LocString description = Description;
        description.Add("Amount", ExtraDeckRemovalMerchantEntry.PriceIncrease);
        NHoverTipSet? tipSet = NHoverTipSet.CreateAndShow(this, new HoverTip(Title, description));
        if (tipSet == null)
        {
            return;
        }

        tipSet.GlobalPosition = GlobalPosition;
        if (GlobalPosition.X > GetViewport().GetVisibleRect().Size.X * 0.5f)
        {
            tipSet.SetAlignment(this, HoverTipAlignment.Left);
            tipSet.GlobalPosition -= Size * 0.5f * Scale;
        }
        else
        {
            tipSet.GlobalPosition += Vector2.Right * Size.X * 0.5f * Scale
                                     + Vector2.Up * Size.Y * 0.5f * Scale;
        }
    }

    private void OnSuccessfulPurchase(PurchaseStatus _, MerchantEntry __)
    {
        TriggerMerchantHandToPointHere();
        UpdateVisual();
    }

    public override void _ExitTree()
    {
        base._ExitTree();

        _entry.EntryUpdated -= UpdateVisual;
        _entry.PurchaseFailed -= OnPurchaseFailed;
        _entry.PurchaseCompleted -= OnSuccessfulPurchase;
    }
}
