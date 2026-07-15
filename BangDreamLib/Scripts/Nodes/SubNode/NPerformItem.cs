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

    private const float HintHighlightExtension = 15f;
    private const float HintHighlightDuration = 0.18f;
    private const float CardEnterBounceDistance = 14f;
    private const float CardEnterPushDuration = 0.06f;
    private const float CardEnterReturnDuration = 0.14f;

    private NCard? _card;
    private NPerformArea? _parent;
    private CardModel? _cardModel;

    private ColorRect? _background;
    private Control? _cardContainer;
    private TextureRect? _cardPortrait;

    private Vector2 _backgroundSize;
    private Vector2 _backgroundTopRight;
    private float _backgroundBaseWidth;
    private bool _isAdjustingBackgroundRect;
    private bool _isHintHighlighted;
    private bool _isCardEnterBouncePending;
    private bool _isCardVisualArrived = true;
    private Vector2 _cardContainerBasePosition;

    private Tween? _fadeInTween;
    private Tween? _fadeOutTween;
    private Tween? _hintHighlightTween;
    private Tween? _cardEnterTween;

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
            if (value == null)
            {
                _isCardVisualArrived = true;
            }
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
        _cardContainer = GetNode<Control>("MarginContainer");
        _cardPortrait = GetNode<TextureRect>("%Portrait");

        _backgroundSize = _background.Size;
        _backgroundTopRight = GetTopRight(_background);
        _backgroundBaseWidth = _background.Size.X;
        _cardContainerBasePosition = _cardContainer.Position;
        _background.ItemRectChanged += OnBackgroundRectChanged;
        _cardPortrait.Resized += UpdatePortraitShaderSize;

        UpdateBackgroundShaderSize();
        UpdatePortraitShaderSize();
        RefreshVisuals();
        ApplyHintHighlightImmediately();
        ConnectSignals();

        if (_isCardEnterBouncePending)
        {
            PlayCardEnterBounce();
        }
    }

    public override void _ExitTree()
    {
        if (_background != null) _background.ItemRectChanged -= OnBackgroundRectChanged;
        if (_cardPortrait != null) _cardPortrait.Resized -= UpdatePortraitShaderSize;

        Model = null;
        Context = null;

        _fadeInTween?.Kill();
        _fadeOutTween?.Kill();
        _hintHighlightTween?.Kill();
        _cardEnterTween?.Kill();

        _card?.QueueFreeSafely();
        _card = null;
    }

    public Vector2 GetSlotGlobalCenter()
    {
        if (_cardContainer == null) return GlobalPosition;

        var localCenter = _cardContainerBasePosition + _cardContainer.Size / 2f;
        return GetGlobalTransform() * localCenter;
    }

    public void PlayCardEnterBounce()
    {
        _isCardVisualArrived = true;
        RefreshVisuals();
        _isCardEnterBouncePending = _cardContainer == null;
        if (_cardContainer == null) return;

        _cardEnterTween?.Kill();
        _cardContainer.Position = _cardContainerBasePosition;

        _cardEnterTween = CreateTween();
        _cardEnterTween.SetPauseMode(Tween.TweenPauseMode.Process);
        _cardEnterTween.TweenProperty(_cardContainer, "position",
                _cardContainerBasePosition + Vector2.Right * CardEnterBounceDistance, CardEnterPushDuration)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        _cardEnterTween.TweenProperty(_cardContainer, "position", _cardContainerBasePosition,
                CardEnterReturnDuration)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);

        var runningTween = _cardEnterTween;
        _cardEnterTween.Finished += () =>
        {
            if (_cardEnterTween == runningTween)
            {
                _cardEnterTween = null;
            }
        };
    }

    public void PrepareCardArrival()
    {
        _isCardVisualArrived = false;
        RefreshVisuals();
    }

    public void SetHintHighlighted(bool highlighted, bool immediately = false)
    {
        if (_isHintHighlighted == highlighted && !immediately) return;

        _isHintHighlighted = highlighted;
        _hintHighlightTween?.Kill();
        _hintHighlightTween = null;
        if (_background == null) return;

        var targetWidth = _backgroundBaseWidth + (highlighted ? HintHighlightExtension : 0f);
        if (immediately)
        {
            SetBackgroundWidth(targetWidth);
            return;
        }

        _hintHighlightTween = CreateTween();
        _hintHighlightTween.SetPauseMode(Tween.TweenPauseMode.Process);
        _hintHighlightTween.TweenProperty(_background, "size:x", targetWidth, HintHighlightDuration)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(highlighted ? Tween.EaseType.Out : Tween.EaseType.InOut);
        _hintHighlightTween.Finished += () => _hintHighlightTween = null;
    }

    private void ApplyHintHighlightImmediately()
    {
        SetBackgroundWidth(_backgroundBaseWidth + (_isHintHighlighted ? HintHighlightExtension : 0f));
    }

    private void SetBackgroundWidth(float width)
    {
        if (_background == null) return;

        var size = _background.Size;
        size.X = width;
        _background.Size = size;
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
            _cardPortrait.Texture = (_isCardVisualArrived ? Model?.Portrait : null) ??
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

            _cardPortrait?.SetInstanceShaderParameter("dot_color", _background.Modulate);
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
