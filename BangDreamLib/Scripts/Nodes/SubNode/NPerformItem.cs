using BangDreamLib.Scripts.Mechanics.Perform;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace BangDreamLib.Scripts.Nodes.SubNode;

/// <summary>
/// 槽位在手动落位选择中的高亮状态。
/// </summary>
public enum PerformSlotSelectionState
{
    /// <summary>未参与选择。</summary>
    None,

    /// <summary>候选槽位。</summary>
    Candidate,

    /// <summary>当前指向的槽位（指定分组时同组槽位同等高亮）。</summary>
    Hovered
}

public partial class NPerformItem : NClickableControl
{
    private static readonly StringName RectSizeShaderParameter = "rect_size";
    private static readonly StringName RevealProgressShaderParameter = "reveal_progress";
    private static readonly StringName DotColorShaderParameter = "dot_color";

    private static readonly Color DefaultColor = PerformSlotColors.Default;

    private const float HintHighlightExtension = 15f;
    private const float HintHighlightDuration = 0.18f;
    private const float CardEnterBounceDistance = 14f;
    private const float CardEnterPushDuration = 0.06f;
    private const float CardEnterReturnDuration = 0.14f;
    private const float PortraitRevealDuration = 0.5f;
    private const float MinimumItemWidth = 160f;
    private const float ItemHeight = 50f;
    private const float BackgroundWidthExtension = 5f;
    private const float TitleHorizontalPadding = 48f;
    private const float PortraitSweepAlpha = 0.75f;
    private const float SelectionCandidateLighten = 0.25f;
    private const float SelectionHoveredLighten = 0.6f;

    private NCard? _card;
    private NPerformArea? _parent;
    private CardModel? _cardModel;

    private ColorRect? _background;
    private Control? _cardContainer;
    private TextureRect? _cardPortrait;
    private ColorRect? _cardOverlay;
    private ColorRect? _portraitSweepLight;
    private Label? _cardTitle;

    private Vector2 _backgroundSize;
    private Vector2 _backgroundTopRight;
    private float _backgroundBaseWidth;
    private bool _isAdjustingBackgroundRect;
    private bool _isHintHighlighted;
    private PerformSlotSelectionState _selectionState = PerformSlotSelectionState.None;
    private bool _isCardEnterBouncePending;
    private bool _isPortraitRevealPending;
    private bool _isWaitingForCardArrival;
    private bool _isMirrored;
    private float _portraitRevealProgress = 1f;
    private Vector2 _cardContainerBasePosition;

    private Tween? _fadeInTween;
    private Tween? _fadeOutTween;
    private Tween? _hintHighlightTween;
    private Tween? _cardEnterTween;
    private Tween? _portraitRevealTween;

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
                _portraitRevealTween?.Kill();
                _portraitRevealTween = null;
                _isPortraitRevealPending = false;
                _isWaitingForCardArrival = false;
                SetPortraitRevealProgress(1f);
            }

            RefreshVisuals();
            if (value != null && !_isWaitingForCardArrival)
            {
                PlayPortraitReveal();
            }
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
        _cardOverlay = GetNode<ColorRect>("%Overlay");
        _portraitSweepLight = GetNode<ColorRect>("%SweepLight");
        _cardTitle = GetNode<Label>("%Title");

        _backgroundSize = _background.Size;
        _backgroundTopRight = GetTopRight(_background);
        _backgroundBaseWidth = _background.Size.X;
        _cardContainerBasePosition = _cardContainer.Position;
        _background.ItemRectChanged += OnBackgroundRectChanged;
        _cardPortrait.Resized += UpdatePortraitShaderSize;
        _cardOverlay.Resized += UpdateOverlayShaderSize;
        _portraitSweepLight.Resized += UpdatePortraitSweepShaderSize;
        _cardTitle.Resized += ApplyTitleMirror;

        UpdateBackgroundShaderSize();
        UpdatePortraitShaderSize();
        UpdateOverlayShaderSize();
        UpdatePortraitSweepShaderSize();
        RefreshVisuals();
        ApplyTitleMirror();
        SetPortraitRevealProgress(_portraitRevealProgress);
        ApplyHintHighlightImmediately();
        ConnectSignals();
        MouseEntered += OnPointerEntered;
        MouseExited += OnPointerExited;

        if (_isPortraitRevealPending)
        {
            PlayPortraitReveal();
        }

        if (_isCardEnterBouncePending)
        {
            PlayCardEnterBounce();
        }
    }

    public override void _ExitTree()
    {
        MouseEntered -= OnPointerEntered;
        MouseExited -= OnPointerExited;

        if (_background != null) _background.ItemRectChanged -= OnBackgroundRectChanged;
        if (_cardPortrait != null) _cardPortrait.Resized -= UpdatePortraitShaderSize;
        if (_cardOverlay != null) _cardOverlay.Resized -= UpdateOverlayShaderSize;
        if (_portraitSweepLight != null) _portraitSweepLight.Resized -= UpdatePortraitSweepShaderSize;
        if (_cardTitle != null) _cardTitle.Resized -= ApplyTitleMirror;

        Model = null;
        Context = null;

        _fadeInTween?.Kill();
        _fadeOutTween?.Kill();
        _hintHighlightTween?.Kill();
        _cardEnterTween?.Kill();
        _portraitRevealTween?.Kill();

        _card?.QueueFreeSafely();
        _card = null;
    }

    public Vector2 GetSlotGlobalCenter()
    {
        if (_cardContainer == null) return GlobalPosition;

        return GetGlobalTransform() * GetContainerLocalCenter();
    }

    /// <summary>槽位根节点内，卡片容器中心的局部坐标。</summary>
    private Vector2 GetContainerLocalCenter()
    {
        return _cardContainerBasePosition + (_cardContainer?.Size ?? Vector2.Zero) / 2f;
    }

    /// <summary>绑定所属演奏区域。场景内置的槽位实例不经 <see cref="Create" />，需在就绪后补绑。</summary>
    internal void AttachToArea(NPerformArea area)
    {
        _parent = area;
    }

    /// <summary>
    /// 设置手动落位选择的高亮状态。与余音槽位提示（<see cref="SetHintHighlighted" />）互不干扰：
    /// 选择高亮只叠加在槽位配色上，由 <see cref="ApplySlotColor" /> 统一呈现。
    /// </summary>
    public void SetSelectionState(PerformSlotSelectionState state)
    {
        if (_selectionState == state) return;

        _selectionState = state;
        ApplySlotColor();
    }

    /// <summary>
    /// 手动落位选择的命中测试。槽位根节点自身尺寸为 0，且命中框右侧的槽位带负的水平缩放，
    /// 因此不能使用 <c>GetGlobalRect</c>，改为按"卡片容器的全局中心 + 缩放后的尺寸"判定。
    /// </summary>
    /// <param name="globalPoint">待判定的全局坐标（通常为鼠标位置）。</param>
    /// <param name="padding">额外放宽的命中边距（像素）。</param>
    public bool TryHitTest(Vector2 globalPoint, float padding = 0f)
    {
        return TryHitTest(globalPoint, padding, out _, out _);
    }

    /// <summary>
    /// 同 <see cref="TryHitTest(Vector2, float)" />，并回带本次判定使用的中心与半尺寸（用于诊断日志）。
    /// </summary>
    public bool TryHitTest(Vector2 globalPoint, float padding, out Vector2 center, out Vector2 halfSize)
    {
        var transform = GetGlobalTransform();
        center = _cardContainer == null ? transform.Origin : transform * GetContainerLocalCenter();

        // 半尺寸按全局变换的基向量长度换算：父级（演奏区/命中框）的缩放一并计入，
        // 只用本节点的 Scale 会在父级带缩放时把命中框算得过小。
        var localHalf = _cardContainer == null
            ? Vector2.Zero
            : _cardContainer.Size * 0.5f + Vector2.One * padding;
        halfSize = new Vector2(localHalf.X * transform.X.Length(), localHalf.Y * transform.Y.Length());

        if (_cardContainer == null || !IsVisibleInTree()) return false;

        var delta = globalPoint - center;
        return Mathf.Abs(delta.X) <= halfSize.X && Mathf.Abs(delta.Y) <= halfSize.Y;
    }

    /// <summary>鼠标指针进入本槽位（供手动落位选择使用）。</summary>
    private void OnPointerEntered()
    {
        _parent?.NotifySlotPointerEntered(this);
    }

    /// <summary>鼠标指针离开本槽位（供手动落位选择使用）。</summary>
    private void OnPointerExited()
    {
        _parent?.NotifySlotPointerExited(this);
    }

    /// <summary>
    /// 应用槽位缩放与水平镜像。演奏区域位于命中框右侧时使用镜像，使左右两侧的槽位
    /// 互为镜像（节点的负水平缩放会一并翻转形状、揭示方向与弹跳方向）。
    /// </summary>
    public void ApplyLayoutScale(float itemScale, bool mirrored)
    {
        _isMirrored = mirrored;
        Scale = new Vector2(mirrored ? -itemScale : itemScale, itemScale);
        ApplyTitleMirror();
    }

    /// <summary>
    /// 抵消镜像对标题文字的影响：文字所在节点再翻转一次，使其保持正读。
    /// </summary>
    private void ApplyTitleMirror()
    {
        if (_cardTitle == null) return;

        _cardTitle.Scale = _isMirrored ? new Vector2(-1f, 1f) : Vector2.One;
        _cardTitle.PivotOffset = _isMirrored ? _cardTitle.Size / 2f : Vector2.Zero;
    }

    public void PlayCardEnterBounce()
    {
        PlayPortraitReveal();
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
        _portraitRevealTween?.Kill();
        _portraitRevealTween = null;
        _isPortraitRevealPending = false;
        _isWaitingForCardArrival = true;
        SetPortraitRevealProgress(0f);
    }

    public void PlayPortraitReveal(float duration = PortraitRevealDuration)
    {
        _portraitRevealTween?.Kill();
        _portraitRevealTween = null;
        _isWaitingForCardArrival = false;
        SetPortraitRevealProgress(0f);

        if (_cardPortrait == null)
        {
            _isPortraitRevealPending = true;
            return;
        }

        _isPortraitRevealPending = false;
        if (duration <= 0f)
        {
            SetPortraitRevealProgress(1f);
            return;
        }

        _portraitRevealTween = CreateTween();
        _portraitRevealTween.SetPauseMode(Tween.TweenPauseMode.Process);
        _portraitRevealTween.TweenMethod(
                Callable.From<double>(progress => SetPortraitRevealProgress((float)progress)),
                0d,
                1d,
                duration)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);

        var runningTween = _portraitRevealTween;
        _portraitRevealTween.Finished += () =>
        {
            SetPortraitRevealProgress(1f);
            if (_portraitRevealTween == runningTween)
            {
                _portraitRevealTween = null;
            }
        };
    }

    private void SetPortraitRevealProgress(float progress)
    {
        _portraitRevealProgress = Math.Clamp(progress, 0f, 1f);
        _cardPortrait?.SetInstanceShaderParameter(RevealProgressShaderParameter, _portraitRevealProgress);
        _cardOverlay?.SetInstanceShaderParameter(RevealProgressShaderParameter, _portraitRevealProgress);
        _portraitSweepLight?.SetInstanceShaderParameter(RevealProgressShaderParameter, _portraitRevealProgress);
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

    private void UpdateOverlayShaderSize()
    {
        _cardOverlay?.SetInstanceShaderParameter(RectSizeShaderParameter, _cardOverlay.Size);
    }

    private void UpdatePortraitSweepShaderSize()
    {
        _portraitSweepLight?.SetInstanceShaderParameter(RectSizeShaderParameter, _portraitSweepLight.Size);
    }

    private void RefreshVisuals()
    {
        if (_cardPortrait != null)
        {
            _cardPortrait.Texture = Model?.Portrait ?? BangDreamPreloadManager.GetTexture2D(
                "res://BangDreamLib/images/sceneui/default_portrait.png");
        }

        if (_cardTitle != null)
        {
            _cardTitle.Text = Model?.Title ?? string.Empty;
        }

        UpdateAdaptiveWidth();
        ApplySlotColor();
    }

    /// <summary>
    /// 重新按演奏方案取色。演奏区域在绑定方案后调用，用于修正早于方案绑定就已就绪的槽位。
    /// </summary>
    public void RefreshSlotColor()
    {
        ApplySlotColor();
    }

    private void ApplySlotColor()
    {
        if (_background == null) return;

        // 槽位配色由角色的演奏方案决定，本节点只负责呈现；选择高亮叠加在其上。
        _background.Modulate = ApplySelectionTint(_parent?.GetSlotColor(this, Model) ?? DefaultColor);

        var dotColor = _background.Modulate;
        dotColor.A = 0.3f;
        _cardOverlay?.SetInstanceShaderParameter(DotColorShaderParameter, dotColor);

        if (_portraitSweepLight != null)
        {
            var sweepColor = _background.Modulate.Lightened(0.45f);
            sweepColor.A = PortraitSweepAlpha;
            _portraitSweepLight.Color = sweepColor;
        }
    }

    /// <summary>把手动选择的高亮叠加到槽位配色上。</summary>
    private Color ApplySelectionTint(Color color)
    {
        return _selectionState switch
        {
            PerformSlotSelectionState.Hovered => color.Lightened(SelectionHoveredLighten),
            PerformSlotSelectionState.Candidate => color.Lightened(SelectionCandidateLighten),
            _ => color
        };
    }

    private void UpdateAdaptiveWidth()
    {
        if (_background == null || _cardContainer == null || _cardPortrait == null || _cardOverlay == null ||
            _cardTitle == null)
            return;

        var titleWidth = _cardTitle.GetThemeFont("font")
            .GetStringSize(
                _cardTitle.Text,
                HorizontalAlignment.Left,
                -1f,
                _cardTitle.GetThemeFontSize("font_size"))
            .X;
        var itemWidth = Mathf.Ceil(Mathf.Max(MinimumItemWidth, titleWidth + TitleHorizontalPadding));
        var itemSize = new Vector2(itemWidth, ItemHeight);

        _cardPortrait.CustomMinimumSize = itemSize;
        _cardOverlay.CustomMinimumSize = itemSize;
        _cardContainer.CustomMinimumSize = itemSize;
        _cardContainer.Size = itemSize;
        _cardContainer.Position = new Vector2(-itemWidth, -ItemHeight / 2f);
        _cardContainerBasePosition = _cardContainer.Position;

        _backgroundBaseWidth = itemWidth + BackgroundWidthExtension;
        _background.CustomMinimumSize = new Vector2(_backgroundBaseWidth, _background.Size.Y);
        SetBackgroundWidth(_backgroundBaseWidth + (_isHintHighlighted ? HintHighlightExtension : 0f));

        UpdateBackgroundShaderSize();
        UpdatePortraitShaderSize();
        UpdateOverlayShaderSize();
        UpdatePortraitSweepShaderSize();
    }

    protected override void OnFocus()
    {
        if (Model == null) return;

        _card ??= NCard.Create(Model);

        if (_card != null && _parent != null)
        {
            if (_card.GetParent() != _parent)
            {
                _parent.AddChildSafely(_card);
            }

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
