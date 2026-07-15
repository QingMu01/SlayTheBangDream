using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace BangDreamLib.Scripts.Nodes.SubNode;

public partial class NPerformItem : NClickableControl
{
    private static readonly StringName RectSizeShaderParameter = "rect_size";

    private static readonly Color DefaultColor = new("#9d9d9d");
    private static readonly Color InstantColor = new("#63a5ff");
    private static readonly Color PerformColor = new("#d30150");

    private NCard? _card;
    private NPerformArea? _parent;
    private CardModel? _cardModel;

    private ColorRect? _background;
    private TextureRect? _cardPortrait;

    private Vector2 _backgroundSize;
    private Vector2 _backgroundTopRight;
    private bool _isAdjustingBackgroundRect;

    private Tween? _fadeInTween;
    private Tween? _fadeOutTween;

    public CardModel? Model
    {
        get => _cardModel;
        set
        {
            if (_cardModel == value) return;

            _cardModel = value;
            _fadeInTween?.Kill();
            _fadeOutTween?.Kill();
            _fadeInTween = null;
            _fadeOutTween = null;
            _card?.QueueFreeSafely();
            _card = null;
            RefreshVisuals();
        }
    }

    public PerformContext? Context { get; set; }

    public static NPerformItem Create(NPerformArea parent, CardModel? model = null)
    {
        var item = PreloadKey.PerformItem.GetScene().Instantiate<NPerformItem>();
        item._parent = parent;

        item.Model = model;
        item.Modulate = parent.Modulate;

        return item;
    }

    public override void _Ready()
    {
        _background = GetNode<ColorRect>("%Background");
        _cardPortrait = GetNode<TextureRect>("%Portrait");

        _backgroundSize = _background.Size;
        _backgroundTopRight = GetTopRight(_background);
        _background.ItemRectChanged += OnBackgroundRectChanged;
        _cardPortrait.Resized += UpdatePortraitShaderSize;

        UpdateBackgroundShaderSize();
        UpdatePortraitShaderSize();
        RefreshVisuals();
        ConnectSignals();
    }

    public override void _ExitTree()
    {
        if (_background != null) _background.ItemRectChanged -= OnBackgroundRectChanged;
        if (_cardPortrait != null) _cardPortrait.Resized -= UpdatePortraitShaderSize;

        Model = null;
        Context = null;

        _fadeInTween?.Kill();
        _fadeOutTween?.Kill();

        _card?.QueueFreeSafely();
        _card = null;
    }

    private static Vector2 GetTopRight(Control control)
    {
        return control.Position + Vector2.Right * control.Size.X;
    }

    private void OnBackgroundRectChanged()
    {
        if (_background == null || _isAdjustingBackgroundRect) return;

        if (!_background.Size.IsEqualApprox(_backgroundSize))
        {
            _isAdjustingBackgroundRect = true;
            _background.Position = _backgroundTopRight - Vector2.Right * _background.Size.X;
            _isAdjustingBackgroundRect = false;
        }

        _backgroundSize = _background.Size;
        _backgroundTopRight = GetTopRight(_background);
        UpdateBackgroundShaderSize();
    }

    private void UpdateBackgroundShaderSize()
    {
        _background?.SetInstanceShaderParameter(RectSizeShaderParameter, _background.Size);
    }

    private void UpdatePortraitShaderSize()
    {
        _cardPortrait?.SetInstanceShaderParameter(RectSizeShaderParameter, _cardPortrait.Size);
    }

    private void RefreshVisuals()
    {
        if (_cardPortrait != null)
        {
            _cardPortrait.Texture = Model?.Portrait ??
                                    PreloadManager.Cache.GetTexture2D(
                                        "res://BangDreamLib/images/sceneui/default_portrait.png");
        }

        if (_background != null)
        {
            if (Model is IPerformCard performCard)
            {
                _background.Modulate = performCard.IsInstant ? InstantColor : PerformColor;
            }
            else
            {
                _background.Modulate = DefaultColor;
            }
        }
    }

    protected override void OnFocus()
    {
        if (Model == null) return;

        _card ??= NCard.Create(Model);

        if (_card != null)
        {
            _parent?.AddChildSafely(_card);

            _card.UpdateVisuals(PileType.Hand, CardPreviewMode.Normal);
            _card.Scale = Vector2.Zero;
            _card.GlobalPosition = GetViewport().GetVisibleRect().GetCenter();

            _fadeOutTween?.Kill();
            _fadeInTween?.Kill();
            _fadeInTween = CreateTween();
            _fadeInTween.TweenProperty(_card, "scale", Vector2.One, 0.2f);
        }
    }

    protected override void OnUnfocus()
    {
        if (!_card.IsValid()) return;

        _fadeInTween?.Kill();
        _fadeOutTween?.Kill();
        _fadeOutTween = CreateTween();
        _fadeOutTween.TweenProperty(_card, "scale", Vector2.Zero, 0.2f);
        _fadeOutTween.Finished += () => { _parent?.RemoveChildSafely(_card); };
    }
}