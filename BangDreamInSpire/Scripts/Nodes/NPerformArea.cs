using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Interfaces.Mechanics;
using BangDreamInSpire.Scripts.Mechanics.Perform;
using BangDreamInSpire.Scripts.Nodes.SubNode;
using BangDreamInSpire.Scripts.Nodes.VFX;
using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using STS2RitsuLib.Combat.SecondaryResources;

namespace BangDreamInSpire.Scripts.Nodes;

/// <summary>按角色演奏方案管理歌单槽位、布局与交互表现。</summary>
public partial class NPerformArea : Control, IPerformArea
{
    private const float ItemHeight = 64f;
    private const float ItemSpacing = 70f;
    private const float ItemApproachSpacing = 25f;

    private const float SlotHitboxGap = 10f;

    private const float LayoutDuration = 0.25f;
    private const float SlotEntranceDuration = 0.3f;
    private const float SlotEntranceDelay = 0.04f;
    private const float SlotEntranceHorizontalOffset = 56f;
    private const float HintFadeDuration = 0.12f;
    private const float HintVerticalOffset = 5f;

    /// <summary>冻结时槽位的降亮系数。</summary>
    private static readonly Color FrozenSlotTint = new(0.82f, 0.84f, 0.92f);

    /// <summary>冻结姿态共用 PerformHangStyle；旋转、位移与镜像分离。</summary>
    private static float GetHangTilt(int slotIndex) => PerformHangStyle.TiltRadians(slotIndex);

    private const float ActivatedBadgeSize = 22f;
    private const string ActivatedBadgeNamePrefix = "ActivatedGroupBadge_";

    private const float SlotSelectionHitPadding = 8f;

    private float _itemScale = 0.7f;

    [Export(PropertyHint.Range, "0.1,2.0,0.01")]
    public float ItemScale
    {
        get => _itemScale;
        set
        {
            _itemScale = Math.Max(0.1f, value);
            ApplyItemScale();
        }
    }

    private Control? _itemContainer;
    private TextureRect? _hint;
    private PanelContainer? _selectionPrompt;
    private Label? _selectionTitle;
    private Label? _selectionDetail;
    private Tween? _selectionPromptTween;
    private Player? _player;
    private IPerformScheme? _scheme;

    private readonly List<NPerformItem> _items = [];
    private readonly List<NPerformItem> _pendingEntranceSlots = [];
    private readonly List<SlotEntranceState> _enteringSlots = [];
    private readonly HashSet<int> _activeSlots = [];
    private readonly List<int> _newlyActiveSlots = [];
    private readonly Dictionary<CardModel, (Transform2D Transform, Vector2 Size)> _lastCardSlotLayouts = [];
    private readonly HashSet<CardModel> _pendingArrivalBounces = [];
    private readonly List<PerformSlotCandidate> _slotSelectionCandidates = [];

    private RunningTween? _layoutTween;
    private RunningTween? _slotEntranceTween;
    private RunningTween? _hintTween;
    private int _slotCount;
    private int _displayedHintAmount;
    private int _targetHintAmount;
    private NPerformItem? _hintHighlightedSlot;
    private float _hintPositionX;
    private bool _isHintInitialized;
    private bool _isHintAnimating;
    private bool _hintNeedsPositionRefresh;
    private bool _isExiting;
    private bool _isApplyingLayout;
    private bool _subscribedToResourceChanged;

    // 核心只计算布局；冻结与激活表现由内容模组通过 PerformAreaVisuals 注册。
    private IPerformAreaFreezeVisual? _freezeVfx;
    private IPerformAreaActivationVisual? _activationVfx;
    private bool _isFrozen;
    private PerformChord _activatedGroups = PerformChord.None;
    private readonly Queue<PerformActivationGroup> _pendingActivationShots = [];
    private Godot.Timer? _activationShotTimer;
    private bool _activationShotPlaying;
    private readonly Dictionary<NPerformItem, Color> _slotBaseModulate = [];
    private readonly Dictionary<PerformChord, ColorRect> _activatedGroupBadges = [];

    private CardModel? _slotSelectionCard;
    private PerformSlotSelectionMode _slotSelectionMode = PerformSlotSelectionMode.None;
    private int _slotSelectionHoveredSlot;
    private int _slotSelectionPointerSlot;

    private sealed record RunningTween(Tween Tween, TaskCompletionSource Completion);

    private sealed record SlotEntranceState(NPerformItem Slot, Vector2 TargetPosition, float TargetAlpha);

    public static NPerformArea Create(Player? player)
    {
        var area = PreloadKey.PerformArea.GetScene().Instantiate<NPerformArea>();
        area._player = player;
        if (player != null && !LocalContext.IsMe(player))
        {
            area.Modulate = new Color(0.5f, 0.5f, 0.5f);
        }

        return area;
    }

    /// <summary>绑定演奏方案，必须在 SyncSlots 前调用。</summary>
    public void SubmitScheme(IPerformScheme scheme)
    {
        _scheme = scheme;
    }

    public override void _Ready()
    {
        _itemContainer = GetNode<Control>("%ItemContainer");
        _hint = GetNode<TextureRect>("%Hint");
        _selectionPrompt = GetNode<PanelContainer>("%SelectionPrompt");
        _selectionTitle = GetNode<Label>("%SelectionTitle");
        _selectionDetail = GetNode<Label>("%SelectionDetail");
        _hintPositionX = _hint.Position.X;
        _items.AddRange(_itemContainer.GetChildren().OfType<NPerformItem>());
        foreach (var item in _items)
        {
            item.AttachToArea(this);
            CaptureSlotModulate(item);
        }

        Visible = _slotCount > 0;
        EnsureSlotCount();
        ApplyItemScale();
        ApplyLayoutImmediately();
        SetProcess(false);
        SetHintTarget(GetHintAmount(), true);
        Callable.From(ApplyDeferredLayout).CallDeferred();
    }

    public void SubmitChanged()
    {
        if (_player == null)
        {
            throw new InvalidOperationException("player is null.");
        }

        if (_scheme is not { UsesSlotHint: true })
        {
            return;
        }

        // 重连或复活时避免重复订阅。
        if (_subscribedToResourceChanged)
        {
            return;
        }

        SecondaryResourceStateStore.Get(_player).Changed += OnSecondaryResourceChanged;
        _subscribedToResourceChanged = true;
    }

    /// <summary>切换冻结表现；解冻时清空分组激活。</summary>
    public void SetFrozen(bool frozen)
    {
        if (_isFrozen == frozen) return;

        _isFrozen = frozen;
        if (!frozen && _activatedGroups != PerformChord.None)
        {
            _activatedGroups = PerformChord.None;
            _pendingActivationShots.Clear();
        }

        ApplyFrozenVisuals();
        ApplyActivatedGroupVisuals();

        // 冻结几何直接落位，避免提线与槽位在缓动期间脱离。
        if (IsInsideTree())
        {
            ApplyLayoutImmediately();
        }
    }

    /// <summary>更新激活分组，并为新增组播放激活表现。</summary>
    public void SetActivatedGroups(PerformChord groups)
    {
        if (_activatedGroups == groups) return;

        var newlyActivated = groups & ~_activatedGroups;
        _activatedGroups = groups;
        if (groups == PerformChord.None)
        {
            // 清空激活时丢弃待播项。
            _pendingActivationShots.Clear();
        }

        ApplyActivatedGroupVisuals();
        PlayGroupActivationOneShot(newlyActivated);
    }

    public bool IsSlotSelectionActive => _slotSelectionCard != null;

    public CardModel? SlotSelectionCard => _slotSelectionCard;

    public int HoveredSlotIndex => _slotSelectionHoveredSlot;

    /// <summary>进入手动落位选择并高亮候选槽位。</summary>
    public void BeginSlotSelection(
        CardModel cardModel,
        PerformSlotSelectionMode mode,
        IReadOnlyList<PerformSlotCandidate> candidates)
    {
        // 防止 BeginDrag 与 TargetSelection 重复提示。
        if (_slotSelectionCard == cardModel && _slotSelectionMode == mode &&
            _slotSelectionCandidates.SequenceEqual(candidates))
        {
            RefreshSlotSelection();
            return;
        }

        EndSlotSelection();
        if (mode == PerformSlotSelectionMode.None || candidates.Count == 0) return;

        _slotSelectionCard = cardModel;
        _slotSelectionMode = mode;
        _slotSelectionCandidates.AddRange(candidates);
        SetProcess(true);
        UpdateSlotSelection(force: true);
        ShowSelectionPrompt();
    }

    /// <summary>立即按鼠标位置刷新指向。</summary>
    public void RefreshSlotSelection()
    {
        if (_slotSelectionCard == null) return;

        UpdateSlotSelection(force: true);
    }

    /// <summary>返回当前落位结果并结束选择。</summary>
    public PerformEnqueueRequest TryConsumeSlotSelection()
    {
        var request = BuildSelectedRequest();
        EndSlotSelection();
        return request;
    }

    public void EndSlotSelection()
    {
        _slotSelectionCard = null;
        _slotSelectionMode = PerformSlotSelectionMode.None;
        _slotSelectionCandidates.Clear();
        _slotSelectionHoveredSlot = 0;
        _slotSelectionPointerSlot = 0;
        SetProcess(false);
        ResetSlotSelectionVisuals();
        HideSelectionPrompt();
        PerformTargetingArrow.End();
    }

    public override void _Process(double delta)
    {
        if (_slotSelectionCard == null) return;

        UpdateSlotSelection(force: false);
        UpdateSelectionPromptPosition();
    }

    /// <summary>刷新候选指向；force 用于立即建立高亮。</summary>
    private void UpdateSlotSelection(bool force)
    {
        RefreshHoveredSlot(force);
    }

    /// <summary>记录 Godot 鼠标拾取，手算命中仅作兜底。</summary>
    internal void NotifySlotPointerEntered(NPerformItem item)
    {
        if (_slotSelectionCard == null) return;

        var slotIndex = _items.IndexOf(item) + 1;
        if (!IsCandidateSlot(slotIndex)) return;

        _slotSelectionPointerSlot = slotIndex;
        RefreshHoveredSlot(force: true);
    }

    internal void NotifySlotPointerExited(NPerformItem item)
    {
        if (_slotSelectionCard == null) return;
        if (_items.IndexOf(item) + 1 != _slotSelectionPointerSlot) return;

        _slotSelectionPointerSlot = 0;
        RefreshHoveredSlot(force: true);
    }

    /// <summary>依次使用鼠标拾取、手算命中与视口坐标判定。</summary>
    private void RefreshHoveredSlot(bool force = false)
    {
        var hovered = _slotSelectionPointerSlot;
        if (hovered <= 0)
        {
            hovered = HitTestCandidateSlot(GetGlobalMousePosition());
        }

        if (hovered <= 0)
        {
            // 坐标空间不一致时用视口坐标兜底。
            var viewportMouse = GetViewport().GetMousePosition();
            if (!viewportMouse.IsEqualApprox(GetGlobalMousePosition()))
            {
                hovered = HitTestCandidateSlot(viewportMouse);
            }
        }

        if (!force && hovered == _slotSelectionHoveredSlot) return;

        _slotSelectionHoveredSlot = hovered;
        ApplySlotSelectionVisuals();

        // 指向候选槽位时高亮原版箭头。
        PerformTargetingArrow.SetHighlighted(hovered > 0);
    }

    private bool IsCandidateSlot(int slotIndex)
    {
        return slotIndex >= 1 &&
               _slotSelectionCandidates.Any(candidate => candidate.SlotIndex == slotIndex);
    }

    public string DescribeSlotSelection()
    {
        var mouse = GetGlobalMousePosition();
        var geometry = _slotSelectionCandidates
            .Where(candidate => candidate.SlotIndex >= 1 && candidate.SlotIndex <= _items.Count)
            .Select(candidate =>
            {
                var item = _items[candidate.SlotIndex - 1];
                item.TryHitTest(mouse, SlotSelectionHitPadding, out var center, out var halfSize);
                var scale = item.GetGlobalTransform().X.Length();
                return $"#{candidate.SlotIndex} origin={item.GlobalPosition} center={center} half={halfSize} " +
                       $"scale={scale:0.###}";
            });

        return $"pointerSlot={_slotSelectionPointerSlot} hoveredSlot={_slotSelectionHoveredSlot} " +
               $"manualSlot={HitTestCandidateSlot(mouse)} mouseCanvas={mouse} " +
               $"mouseViewport={GetViewport().GetMousePosition()} area={GlobalPosition} " +
               $"[{string.Join("; ", geometry)}]";
    }

    /// <summary>后绘制槽位优先，忽略未激活槽位。</summary>
    private int HitTestCandidateSlot(Vector2 globalPoint)
    {
        for (var index = _slotSelectionCandidates.Count - 1; index >= 0; index--)
        {
            var slotIndex = _slotSelectionCandidates[index].SlotIndex;
            if (slotIndex < 1 || slotIndex > _items.Count) continue;

            var item = _items[slotIndex - 1];
            if (item.TryHitTest(globalPoint, SlotSelectionHitPadding)) return slotIndex;
        }

        return 0;
    }

    private PerformEnqueueRequest BuildSelectedRequest()
    {
        if (_slotSelectionHoveredSlot <= 0) return default;
        if (_slotSelectionMode == PerformSlotSelectionMode.Slot)
        {
            return PerformEnqueueRequest.Slot(_slotSelectionHoveredSlot);
        }

        var candidate = _slotSelectionCandidates
            .FirstOrDefault(candidate => candidate.SlotIndex == _slotSelectionHoveredSlot);
        return PerformEnqueueRequest.ForGroup(candidate.Group);
    }

    /// <summary>刷新候选高亮，分组模式同时高亮同组候选。</summary>
    private void ApplySlotSelectionVisuals()
    {
        var highlightsGroup = _slotSelectionMode == PerformSlotSelectionMode.Group && _slotSelectionHoveredSlot > 0;
        var hoveredCandidate = highlightsGroup
            ? _slotSelectionCandidates.FirstOrDefault(candidate => candidate.SlotIndex == _slotSelectionHoveredSlot)
            : default;

        foreach (var candidate in _slotSelectionCandidates)
        {
            if (candidate.SlotIndex < 1 || candidate.SlotIndex > _items.Count) continue;

            var isHovered = _slotSelectionHoveredSlot > 0 &&
                            (candidate.SlotIndex == _slotSelectionHoveredSlot ||
                             highlightsGroup && candidate.Group == hoveredCandidate.Group);
            _items[candidate.SlotIndex - 1].SetSelectionState(isHovered
                ? PerformSlotSelectionState.Hovered
                : PerformSlotSelectionState.Candidate);
        }

        UpdateSelectionPromptText();
    }

    private void ShowSelectionPrompt()
    {
        if (_selectionPrompt == null) return;

        _selectionPromptTween?.Kill();
        _selectionPrompt.Visible = true;
        UpdateSelectionPromptPosition();
        _selectionPromptTween = CreateTween();
        _selectionPromptTween.SetParallel();
        _selectionPromptTween.TweenProperty(_selectionPrompt, "modulate:a", 1f, 0.16f).From(0f);
        _selectionPromptTween.TweenProperty(_selectionPrompt, "scale", Vector2.One, 0.2f)
            .From(Vector2.One * 0.94f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
    }

    private void HideSelectionPrompt()
    {
        _selectionPromptTween?.Kill();
        _selectionPromptTween = null;
        if (_selectionPrompt != null) _selectionPrompt.Visible = false;
    }

    private void UpdateSelectionPromptText()
    {
        if (_selectionTitle == null || _selectionDetail == null) return;

        const string prefix = "BANG_DREAM_IN_SPIRE_PERFORM_TARGET_";
        var isGroup = _slotSelectionMode == PerformSlotSelectionMode.Group;
        _selectionTitle.Text = new LocString("gameplay_ui", prefix + (isGroup ? "GROUP" : "SLOT"))
            .GetFormattedText();

        var detailKey = "AIM";
        if (_slotSelectionHoveredSlot > 0)
        {
            detailKey = isGroup ? "GROUP_READY" :
                _items[_slotSelectionHoveredSlot - 1].Model == null ? "SLOT_READY" : "REPLACE_READY";
        }

        var detail = new LocString("gameplay_ui", prefix + detailKey);
        detail.Add("Slot", _slotSelectionHoveredSlot);
        _selectionDetail.Text = detail.GetFormattedText();
    }

    /// <summary>在候选槽位上方显示不镜像的操作提示。</summary>
    private void UpdateSelectionPromptPosition()
    {
        if (_selectionPrompt == null) return;

        var inverse = GetGlobalTransform().AffineInverse();
        Rect2? bounds = null;
        foreach (var candidate in _slotSelectionCandidates)
        {
            if (candidate.SlotIndex < 1 || candidate.SlotIndex > _items.Count) continue;
            var item = _items[candidate.SlotIndex - 1];
            if (!item.TryHitTest(item.GetSlotGlobalCenter(), 0f, out var center, out var halfSize)) continue;

            var rect = new Rect2(inverse * (center - halfSize), Vector2.Zero)
                .Expand(inverse * (center + halfSize));
            bounds = bounds?.Merge(rect) ?? rect;
        }

        if (bounds == null) return;
        _selectionPrompt.Size = _selectionPrompt.GetCombinedMinimumSize();
        _selectionPrompt.PivotOffset = _selectionPrompt.Size / 2f;
        var position = new Vector2(
            bounds.Value.GetCenter().X - _selectionPrompt.Size.X / 2f,
            bounds.Value.Position.Y - _selectionPrompt.Size.Y - 18f);

        // 屏幕边缘也须完整显示提示。
        var viewport = GetViewport().GetVisibleRect().Grow(-12f);
        var canvasInverse = GetGlobalTransformWithCanvas().AffineInverse();
        var visible = new Rect2(canvasInverse * viewport.Position, Vector2.Zero)
            .Expand(canvasInverse * viewport.End);
        position.X = Mathf.Clamp(position.X, visible.Position.X,
            Mathf.Max(visible.Position.X, visible.End.X - _selectionPrompt.Size.X));
        position.Y = Mathf.Clamp(position.Y, visible.Position.Y,
            Mathf.Max(visible.Position.Y, visible.End.Y - _selectionPrompt.Size.Y));
        _selectionPrompt.Position = position;
    }

    private void ResetSlotSelectionVisuals()
    {
        foreach (var item in _items)
        {
            item.SetSelectionState(PerformSlotSelectionState.None);
        }
    }

    public override void _ExitTree()
    {
        _isExiting = true;
        _pendingActivationShots.Clear();
        _activationShotPlaying = false;
        _slotSelectionCard = null;
        _slotSelectionMode = PerformSlotSelectionMode.None;
        _slotSelectionCandidates.Clear();
        _slotSelectionHoveredSlot = 0;
        _slotSelectionPointerSlot = 0;
        SetProcess(false);
        HideSelectionPrompt();
        PerformTargetingArrow.End();

        if (_subscribedToResourceChanged && _player != null &&
            SecondaryResourceStateStore.TryGet(_player, out var resourceState))
        {
            resourceState.Changed -= OnSecondaryResourceChanged;
        }

        _subscribedToResourceChanged = false;

        CancelLayoutTween();
        CancelSlotEntranceTween(false);
        CancelHintTween();
        SetHintHighlightedSlot(null, true);
        _lastCardSlotLayouts.Clear();
        _pendingArrivalBounces.Clear();
    }

    /// <summary>同步槽位总数、激活集合与布局。</summary>
    public void SyncSlots(int totalSlotCount, IReadOnlyCollection<int> activeSlots)
    {
        var nextSlotCount = Math.Max(0, totalSlotCount);
        var nextActiveSlots = activeSlots.ToHashSet();
        var changed = _slotCount != nextSlotCount || !_activeSlots.SetEquals(nextActiveSlots);

        _slotCount = nextSlotCount;
        Visible = _slotCount > 0 && nextActiveSlots.Count > 0;

        ApplyItemScale();

        if (!changed) return;

        foreach (var slotIndex in nextActiveSlots.Where(slotIndex => !_activeSlots.Contains(slotIndex)))
        {
            _newlyActiveSlots.Add(slotIndex);
        }

        _activeSlots.Clear();
        _activeSlots.UnionWith(nextActiveSlots);

        TaskHelper.RunSafely(ApplySlotSync());
    }

    public void AddItem(CardModel cardModel, PerformContext context, bool waitForCardArrival)
    {
        if (_itemContainer == null || _items.Any(item => item.Model == cardModel)) return;
        if (context.SlotIndex < 1 || context.SlotIndex > _items.Count) return;

        var slot = _items[context.SlotIndex - 1];
        if (slot.Model != null) return;

        var cardArrivalAlreadyFinished = _pendingArrivalBounces.Remove(cardModel);
        if (waitForCardArrival || cardArrivalAlreadyFinished)
        {
            slot.PrepareCardArrival();
        }

        slot.Model = cardModel;
        slot.Context = context;
        context.Slot = slot;
        CacheCardSlotLayout(cardModel, slot);
        if (cardArrivalAlreadyFinished)
        {
            slot.PlayCardEnterBounce();
        }
    }

    public void PlayCardArrivalBounce(CardModel cardModel)
    {
        var slot = _items.FirstOrDefault(item => item.Model == cardModel);
        if (slot != null)
        {
            slot.PlayCardEnterBounce();
        }
        else if (cardModel.Pile?.Type == BangDreamConst.PerformPile)
        {
            _pendingArrivalBounces.Add(cardModel);
        }
    }

    public void RemoveItem(CardModel cardModel)
    {
        var item = _items.FirstOrDefault(candidate => candidate.Model == cardModel);
        _pendingArrivalBounces.Remove(cardModel);
        if (item == null) return;

        CacheCardSlotLayout(cardModel, item);

        if (item.Context != null)
        {
            item.Context.Slot = null;
        }

        item.Context = null;
        item.Model = null;
    }

    public bool TryGetCardSlotCenter(CardModel cardModel, out Vector2 center)
    {
        return TryGetCardSlotLayout(cardModel, out center, out _);
    }

    /// <summary>返回槽位中心与未缩放尺寸；移除后沿用最后布局。</summary>
    public bool TryGetCardSlotLayout(CardModel cardModel, out Vector2 center, out Vector2 size)
    {
        var found = TryGetCardSlotTransform(cardModel, out var transform, out size);
        center = transform.Origin;
        return found;
    }

    public bool IsSlotLayoutPlaying => (_layoutTween?.Tween.IsRunning() ?? false) ||
                                       (_slotEntranceTween?.Tween.IsRunning() ?? false);

    /// <inheritdoc />
    public Transform2D GlobalTransform => GetGlobalTransform();

    /// <inheritdoc />
    public Task AwaitProcessFrame() => NodeUtil.AwaitProcessFrame(this);

    /// <inheritdoc />
    public void AddVfx(Node vfx) => this.AddChildSafely(vfx);

    private const string PerformFlashVfxPath = "res://BangDreamInSpire/scenes/vfx/perform_flash_vfx.tscn";

    /// <inheritdoc />
    public void PlayFlashVfx(CardModel card)
    {
        if (!IsInsideTree()) return;
        if (!TryGetCardSlotTransform(card, out var slotTransform, out var slotSize)) return;

        var slot = _items.FirstOrDefault(item => item.Model == card);
        if (slot != null)
        {
            slot.SuppressPortraitSweep();
        }

        var relativeTransform = GlobalTransform.AffineInverse() * slotTransform;
        if (TryPlaySealVfx(card, slotSize, relativeTransform))
        {
            return;
        }

        var vfx = BangDreamPreloadManager.GetScene(PerformFlashVfxPath).Instantiate<PerformFlashVfx>();
        vfx.FlashColor = slot != null ? GetSlotColor(slot, card) : PerformSlotColors.Default;
        vfx.ConfigureSlot(slotSize, relativeTransform);
        AddVfx(vfx);
    }

    /// <summary>元素印牌走专属演奏反馈；场景缺失时回落通用扫光，不吞掉演奏反馈。</summary>
    private bool TryPlaySealVfx(CardModel card, Vector2 slotSize, Transform2D relativeTransform)
    {
        if (card is not IPerformSealCard sealCard) return false;

        var scenePath = PerformSealScenes.ScenePathOf(sealCard.PerformSeal);
        if (scenePath == null || !ResourceLoader.Exists(scenePath)) return false;

        var vfx = BangDreamPreloadManager.GetScene(scenePath).Instantiate<PerformSealVfx>();
        vfx.ConfigureSlot(slotSize, relativeTransform);
        AddVfx(vfx);
        return true;
    }

    /// <summary>返回保留父级缩放与镜像的槽位中心变换。</summary>
    public bool TryGetCardSlotTransform(CardModel cardModel, out Transform2D transform, out Vector2 size)
    {
        transform = Transform2D.Identity;
        size = Vector2.Zero;
        var activeSlot = _items.FirstOrDefault(item => item.Model == cardModel);
        if (activeSlot != null)
        {
            (transform, size) = CacheCardSlotLayout(cardModel, activeSlot);
            return true;
        }

        if (_player == cardModel.Owner)
        {
            var slotIndex = _player.AttachedData().PerformManager.GetExpectedSlotIndex(cardModel);
            if (slotIndex >= 1 && slotIndex <= _items.Count && _activeSlots.Contains(slotIndex))
            {
                var slot = _items[slotIndex - 1];
                (transform, size) = CacheCardSlotLayout(cardModel, slot);
                return true;
            }
        }

        if (!_lastCardSlotLayouts.TryGetValue(cardModel, out var layout)) return false;

        (transform, size) = layout;
        return true;
    }

    private (Transform2D Transform, Vector2 Size) CacheCardSlotLayout(CardModel cardModel, NPerformItem slot)
    {
        var transform = slot.GetGlobalTransform();
        transform.Origin = slot.GetSlotGlobalCenter();
        var layout = (transform, slot.LocalSize);
        _lastCardSlotLayouts[cardModel] = layout;
        return layout;
    }

    /// <summary>批量调整后平滑重排现有槽位。</summary>
    public void RefreshItemLayout()
    {
        ReassignItemsToSlots();
        ApplyItemVisibility();
        TaskHelper.RunSafely(AnimateLayout());
    }

    private async Task ApplySlotSync()
    {
        // 退树后不再为槽位创建补间。
        if (_isExiting || !IsInsideTree())
        {
            return;
        }

        EnsureSlotCount();

        foreach (var slotIndex in _newlyActiveSlots)
        {
            if (slotIndex >= 1 && slotIndex <= _items.Count && !_pendingEntranceSlots.Contains(_items[slotIndex - 1]))
            {
                _pendingEntranceSlots.Add(_items[slotIndex - 1]);
            }
        }

        _newlyActiveSlots.Clear();
        ApplyItemVisibility();

        await AnimatePendingSlotEntrances();
        await AnimateLayout();
        SetHintTarget(GetHintAmount(), false, true);
    }

    private Task AnimateLayout()
    {
        if (_isExiting || !IsInsideTree()) return Task.CompletedTask;
        if (_itemContainer == null) return Task.CompletedTask;

        CancelSlotEntranceTween(true);
        CancelLayoutTween();
        var orderedSlotIndexes = _items.Count == 0
            ? []
            : Enumerable.Range(1, _items.Count).Where(_activeSlots.Contains).ToList();
        if (orderedSlotIndexes.Count == 0) return Task.CompletedTask;

        var tween = CreateTween();
        tween.SetParallel();
        tween.SetPauseMode(Tween.TweenPauseMode.Process);

        foreach (var slotIndex in orderedSlotIndexes)
        {
            var item = _items[slotIndex - 1];
            if (!item.IsInsideTree()) continue;

            tween.TweenProperty(item, "position", GetItemPosition(slotIndex), LayoutDuration)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.InOut);
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runningTween = new RunningTween(tween, completion);
        _layoutTween = runningTween;
        tween.Finished += () =>
        {
            completion.TrySetResult();
            if (_layoutTween == runningTween)
            {
                _layoutTween = null;
            }

            // 移动结束后重贴冻结层。
            if (_isFrozen) ApplyFrozenVisuals();
        };

        return completion.Task;
    }

    private void ApplyLayoutImmediately()
    {
        if (_itemContainer == null || _isApplyingLayout) return;

        // 先落位再刷新视觉几何；冻结姿态直接应用，避免提线与槽位缓动脱节。
        // _isApplyingLayout 防止与刷新方法相互调用而递归。
        _isApplyingLayout = true;
        try
        {
            ApplySlotPositions();
            ApplyItemVisibility();
            UpdateHintPosition();
        }
        finally
        {
            _isApplyingLayout = false;
        }
    }

    private void ApplySlotPositions()
    {
        for (var slotIndex = 1; slotIndex <= _items.Count; slotIndex++)
        {
            if (!_activeSlots.Contains(slotIndex)) continue;

            var item = _items[slotIndex - 1];
            item.Position = GetItemPosition(slotIndex);
            item.SetHangTilt(_isFrozen ? GetHangTilt(slotIndex) : 0f);
        }
    }

    /// <summary>刷新槽位显隐与视觉层；不得回调 ApplyLayoutImmediately 以免递归。</summary>
    private void ApplyItemVisibility()
    {
        for (var index = 0; index < _items.Count; index++)
        {
            _items[index].Visible = _activeSlots.Contains(index + 1);
        }

        ApplyFrozenVisuals();
        ApplyActivatedGroupVisuals();
    }

    /// <summary>记录原始 modulate 供解冻恢复。</summary>
    private void CaptureSlotModulate(NPerformItem item)
    {
        _slotBaseModulate.TryAdd(item, item.Modulate);
    }

    private Color GetSlotBaseModulate(NPerformItem item)
    {
        CaptureSlotModulate(item);
        return _slotBaseModulate[item];
    }

    /// <summary>应用降亮并向内容模组提供冻结行布局。</summary>
    private void ApplyFrozenVisuals()
    {
        foreach (var item in _items)
        {
            var baseModulate = GetSlotBaseModulate(item);
            item.Modulate = _isFrozen ? baseModulate * FrozenSlotTint : baseModulate;
        }

        if (!_isFrozen)
        {
            if (_freezeVfx != null && GodotObject.IsInstanceValid(_freezeVfx.View))
            {
                _freezeVfx.PlayHide();
            }

            _freezeVfx = null;
            return;
        }

        var groups = BuildFreezeGroups();
        if (groups.Count == 0)
        {
            return;
        }

        if (_freezeVfx == null || !GodotObject.IsInstanceValid(_freezeVfx.View))
        {
            _freezeVfx = PerformAreaVisuals.CreateFreezeVisual();
            if (_freezeVfx == null)
            {
                return;
            }

            _itemContainer?.AddChild(_freezeVfx.View);
            _freezeVfx.SetGroups(groups);
            _freezeVfx.PlayShow();
            return;
        }

        _freezeVfx.SetGroups(groups);
    }

    private List<PerformRowGroup> BuildFreezeGroups()
    {
        var groups = new List<PerformRowGroup>();
        if (_scheme == null || _itemContainer == null) return groups;

        foreach (var descriptor in _scheme.Groups)
        {
            if (!TryGetGroupRows(descriptor, out var rows, out var mirrored)) continue;
            groups.Add(new PerformRowGroup(descriptor.Chord, rows, mirrored));
        }

        return groups;
    }

    /// <summary>返回分组行范围、倾角与镜像信息。</summary>
    private bool TryGetGroupRows(PerformGroupDescriptor descriptor, out PerformRow[] rows, out bool mirrored)
    {
        rows = [];
        mirrored = IsMirroredCorner(GetCorner(descriptor.BaseSlotIndex));

        var collected = new List<PerformRow>(descriptor.MaxSlots);
        for (var local = 0; local < descriptor.MaxSlots; local++)
        {
            var slotIndex = descriptor.BaseSlotIndex + local;
            if (slotIndex < 1 || slotIndex > _items.Count) continue;

            var item = _items[slotIndex - 1];
            if (!item.Visible) continue;

            var scale = item.Scale;
            var size = new Vector2(
                Mathf.Abs(item.LocalSize.X * scale.X),
                Mathf.Abs(item.LocalSize.Y * scale.Y));
            if (size.X <= 0f || size.Y <= 0f) continue;

            // 镜像列的延展方向相反，避免矩形投到角色身上。
            var position = item.Position + (scale.X < 0f ? Vector2.Zero : new Vector2(-size.X, 0f));
            collected.Add(new PerformRow(new Rect2(position, size), item.HangTilt));
        }

        if (collected.Count == 0) return false;

        rows = collected.ToArray();
        return true;
    }

    /// <summary>返回激活提线终点的容器局部坐标。</summary>
    private Vector2 GetPerformTargetCenter()
    {
        return new Vector2(
            (GetHitboxLeftX() + GetHitboxRightX()) * 0.5f,
            (GetHitboxTopY() + GetHitboxBottomY()) * 0.5f);
    }

    /// <summary>更新角标与内容模组激活层。</summary>
    private void ApplyActivatedGroupVisuals()
    {
        if (_itemContainer == null) return;

        var states = BuildActivationStates();
        if (states.Count > 0)
        {
            EnsureActivationVfx()?.SetActive(states);
        }
        else if (_activationVfx != null && GodotObject.IsInstanceValid(_activationVfx.View))
        {
            // 解冻或复活时清除常驻激活表现。
            _activationVfx.Clear();
        }

        foreach (var group in _activatedGroups.EnumerateGroups())
        {
            if (!TryGetGroupBadgeAnchor(group, out var position))
            {
                HideBadge(group);
                continue;
            }

            var badge = EnsureActivatedBadge(group);
            badge.Position = position;
            badge.Visible = _isFrozen;
        }

        foreach (var group in _activatedGroupBadges.Keys.ToList())
        {
            if (!_activatedGroups.HasFlag(group)) HideBadge(group);
        }
    }

    private ColorRect EnsureActivatedBadge(PerformChord group)
    {
        if (_activatedGroupBadges.TryGetValue(group, out var existing) && GodotObject.IsInstanceValid(existing))
        {
            return existing;
        }

        var badge = new ColorRect
        {
            Name = ActivatedBadgeNamePrefix + group,
            Color = PerformChordColors.ColorOf(group),
            Size = new Vector2(ActivatedBadgeSize, ActivatedBadgeSize),
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 6,
            Visible = false
        };

        var label = new Label
        {
            Name = "Label",
            Text = group.ToString(),
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.SetAnchorsPreset(LayoutPreset.FullRect);
        badge.AddChild(label);

        _itemContainer!.AddChild(badge);
        _activatedGroupBadges[group] = badge;
        return badge;
    }

    private void HideBadge(PerformChord group)
    {
        if (_activatedGroupBadges.TryGetValue(group, out var badge) && GodotObject.IsInstanceValid(badge))
        {
            badge.Visible = false;
        }
    }

    /// <summary>返回激活层所需的行、角标与角色坐标。</summary>
    private List<PerformActivationGroup> BuildActivationStates()
    {
        var states = new List<PerformActivationGroup>();
        if (_scheme == null) return states;

        var target = GetPerformTargetCenter();
        foreach (var descriptor in _scheme.Groups)
        {
            if (!_activatedGroups.HasFlag(descriptor.Chord)) continue;
            if (!TryGetGroupRows(descriptor, out var rows, out var mirrored)) continue;
            if (!TryGetGroupBadgeAnchor(descriptor.Chord, out var badge)) continue;

            states.Add(new PerformActivationGroup(
                descriptor.Chord, rows, badge, target, mirrored));
        }

        return states;
    }

    /// <summary>按 OneShotLifeTime 串行播放新增分组，避免单通道表现互相覆盖。</summary>
    private void PlayGroupActivationOneShot(PerformChord newlyActivated)
    {
        if (newlyActivated == PerformChord.None || _scheme == null) return;

        var target = GetPerformTargetCenter();
        foreach (var descriptor in _scheme.Groups)
        {
            if (!newlyActivated.HasFlag(descriptor.Chord)) continue;
            if (!TryGetGroupRows(descriptor, out var rows, out var mirrored)) continue;
            if (!TryGetGroupBadgeAnchor(descriptor.Chord, out var badge)) continue;

            _pendingActivationShots.Enqueue(new PerformActivationGroup(
                descriptor.Chord, rows, badge, target, mirrored));
        }

        TryPlayNextActivationShot();
    }

    private void TryPlayNextActivationShot()
    {
        if (_activationShotPlaying || _isExiting || _pendingActivationShots.Count == 0) return;

        var activationVfx = EnsureActivationVfx();
        if (activationVfx == null)
        {
            _pendingActivationShots.Clear();
            return;
        }

        _activationShotPlaying = true;
        activationVfx.PlayOneShot(_pendingActivationShots.Dequeue());

        if (_activationShotTimer == null)
        {
            _activationShotTimer = new Godot.Timer { OneShot = true };
            _activationShotTimer.Timeout += OnActivationShotTimeout;
            AddChild(_activationShotTimer);
        }

        _activationShotTimer.Start(activationVfx.OneShotLifeTime);
    }

    private void OnActivationShotTimeout()
    {
        _activationShotPlaying = false;
        if (_isExiting) return;
        TryPlayNextActivationShot();
    }

    private IPerformAreaActivationVisual? EnsureActivationVfx()
    {
        if (_activationVfx != null && GodotObject.IsInstanceValid(_activationVfx.View))
        {
            return _activationVfx;
        }

        _activationVfx = PerformAreaVisuals.CreateActivationVisual();
        if (_activationVfx == null)
        {
            return null;
        }

        _itemContainer?.AddChild(_activationVfx.View);
        return _activationVfx;
    }

    /// <summary>将角标放在分组首个激活槽位外侧。</summary>
    private bool TryGetGroupBadgeAnchor(PerformChord group, out Vector2 position)
    {
        position = Vector2.Zero;
        if (_scheme == null || group == PerformChord.None) return false;

        foreach (var descriptor in _scheme.Groups)
        {
            if (descriptor.Chord != group) continue;

            for (var local = 0; local < descriptor.MaxSlots; local++)
            {
                var slotIndex = descriptor.BaseSlotIndex + local;
                if (!_activeSlots.Contains(slotIndex) || slotIndex < 1 || slotIndex > _items.Count) continue;

                var offset = GetCorner(slotIndex) switch
                {
                    PerformSlotCorner.TopLeft => new Vector2(-ActivatedBadgeSize - 4f, -6f),
                    PerformSlotCorner.BottomLeft => new Vector2(-ActivatedBadgeSize - 4f, 6f),
                    PerformSlotCorner.TopRight => new Vector2(4f, -6f),
                    PerformSlotCorner.BottomRight => new Vector2(4f, 6f),
                    _ => new Vector2(0f, -ActivatedBadgeSize - 4f)
                };
                position = _items[slotIndex - 1].Position + offset;
                return true;
            }

            return false;
        }

        return false;
    }

    private void EnsureSlotCount()
    {
        if (_itemContainer == null) return;

        while (_items.Count < _slotCount)
        {
            var slotIndex = _items.Count + 1;
            var slot = NPerformItem.Create(this);
            slot.Position = GetItemPosition(slotIndex);
            ApplySlotScale(slot, slotIndex);
            // 新槽位先记录原始 modulate，供冻结恢复。
            CaptureSlotModulate(slot);
            _items.Add(slot);
            _itemContainer.AddChild(slot);
            if (_activeSlots.Contains(slotIndex))
            {
                _pendingEntranceSlots.Add(slot);
            }
        }

        while (_items.Count > _slotCount)
        {
            var slot = _items[^1];
            if (slot.Context != null)
            {
                slot.Context.Slot = null;
            }

            _items.RemoveAt(_items.Count - 1);
            _pendingEntranceSlots.Remove(slot);
            _slotBaseModulate.Remove(slot);
            slot.Visible = false;
            // 先解除模型引用再释放槽位。
            slot.Model = null;
            slot.QueueFree();
        }
    }

    private Task AnimatePendingSlotEntrances()
    {
        if (_itemContainer == null || _pendingEntranceSlots.Count == 0 || _isExiting)
        {
            return Task.CompletedTask;
        }

        CancelSlotEntranceTween(true);
        _enteringSlots.Clear();

        foreach (var slot in _pendingEntranceSlots.ToList())
        {
            var slotIndex = _items.IndexOf(slot) + 1;
            if (slotIndex < 1 || !slot.IsInsideTree()) continue;

            var targetPosition = GetItemPosition(slotIndex);
            var targetAlpha = slot.Modulate.A;
            _enteringSlots.Add(new SlotEntranceState(slot, targetPosition, targetAlpha));
            // 从命中框外侧滑入。
            var outward = IsMirroredCorner(GetCorner(slotIndex)) ? Vector2.Right : Vector2.Left;
            slot.Position = targetPosition + outward * SlotEntranceHorizontalOffset;
            SetItemAlpha(slot, 0f);
        }

        _pendingEntranceSlots.Clear();
        if (_enteringSlots.Count == 0) return Task.CompletedTask;

        var tween = CreateTween();
        tween.SetParallel();
        tween.SetPauseMode(Tween.TweenPauseMode.Process);

        for (var index = 0; index < _enteringSlots.Count; index++)
        {
            var state = _enteringSlots[index];
            var delay = index * SlotEntranceDelay;
            tween.TweenProperty(state.Slot, "position", state.TargetPosition, SlotEntranceDuration)
                .SetDelay(delay)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(state.Slot, "modulate:a", state.TargetAlpha, SlotEntranceDuration * 0.8f)
                .SetDelay(delay)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runningTween = new RunningTween(tween, completion);
        _slotEntranceTween = runningTween;
        tween.Finished += () =>
        {
            CompleteSlotEntrances();
            completion.TrySetResult();
            if (_slotEntranceTween == runningTween)
            {
                _slotEntranceTween = null;
            }
        };

        return completion.Task;
    }

    private static void SetItemAlpha(NPerformItem item, float alpha)
    {
        var modulate = item.Modulate;
        modulate.A = alpha;
        item.Modulate = modulate;
    }

    private void CompleteSlotEntrances()
    {
        foreach (var state in _enteringSlots)
        {
            if (!IsInstanceValid(state.Slot)) continue;
            state.Slot.Position = state.TargetPosition;
            SetItemAlpha(state.Slot, state.TargetAlpha);
        }

        _enteringSlots.Clear();
    }

    private void ApplyItemScale()
    {
        for (var index = 0; index < _items.Count; index++)
        {
            var item = _items[index];
            ApplySlotScale(item, index + 1);
            item.RefreshSlotColor();
        }
    }

    private void ReassignItemsToSlots()
    {
        var assignments = _items
            .Where(slot => slot is { Model: not null, Context: not null })
            .Select(slot => (slot.Model!, slot.Context!))
            .ToList();

        foreach (var slot in _items)
        {
            if (slot.Context != null)
            {
                slot.Context.Slot = null;
            }

            slot.Model = null;
            slot.Context = null;
        }

        foreach (var (cardModel, context) in assignments)
        {
            if (context.SlotIndex < 1 || context.SlotIndex > _items.Count)
            {
                context.Slot = null;
                continue;
            }

            var slot = _items[context.SlotIndex - 1];
            slot.Model = cardModel;
            slot.Context = context;
            context.Slot = slot;
        }
    }

    /// <summary>计算槽位原点。未镜像物品以右缘贴锚点，镜像物品以左缘贴锚点，
    /// 间距不随卡名宽度变化。</summary>
    private Vector2 GetItemPosition(int slotIndex)
    {
        if (_itemContainer == null) return Vector2.Zero;

        var anchor = _scheme?.GetSlotAnchor(slotIndex) ??
                     new PerformSlotAnchor(PerformSlotCorner.BottomCenter, slotIndex - 1);
        var stack = anchor.Offset * ItemSpacing * _itemScale;
        var approach = anchor.Offset * ItemApproachSpacing * _itemScale;
        var bottom = GetHitboxBottomY() - _itemContainer.Position.Y;

        var basePosition = anchor.Corner switch
        {
            PerformSlotCorner.TopLeft => new Vector2(
                GetHitboxLeftX() - SlotHitboxGap, GetHitboxTopY() + ItemHeight / 2f + stack),
            PerformSlotCorner.BottomLeft => new Vector2(
                GetHitboxLeftX() - SlotHitboxGap, bottom - ItemHeight / 2f - stack),
            PerformSlotCorner.TopRight => new Vector2(
                GetHitboxRightX() + SlotHitboxGap, GetHitboxTopY() + ItemHeight / 2f + stack),
            PerformSlotCorner.BottomRight => new Vector2(
                GetHitboxRightX() + SlotHitboxGap, bottom - ItemHeight / 2f - stack),
            _ => new Vector2(approach, bottom - ItemHeight / 2f - stack)
        };

        return _isFrozen
            ? basePosition + PerformHangStyle.Offset(slotIndex, IsMirroredCorner(anchor.Corner))
            : basePosition;
    }

    private PerformSlotCorner GetCorner(int slotIndex)
    {
        return (_scheme?.GetSlotAnchor(slotIndex) ??
                new PerformSlotAnchor(PerformSlotCorner.BottomCenter, slotIndex - 1)).Corner;
    }

    /// <summary>右侧槽位水平镜像。</summary>
    private static bool IsMirroredCorner(PerformSlotCorner corner)
    {
        return corner is PerformSlotCorner.TopRight or PerformSlotCorner.BottomRight;
    }

    private void ApplySlotScale(NPerformItem item, int slotIndex)
    {
        item.ApplyLayoutScale(_itemScale, IsMirroredCorner(GetCorner(slotIndex)));
        item.SetHangTilt(_isFrozen ? GetHangTilt(slotIndex) : 0f);
    }

    /// <summary>按演奏方案返回槽位颜色。</summary>
    internal Color GetSlotColor(NPerformItem item, CardModel? card)
    {
        var slotIndex = _items.IndexOf(item) + 1;
        return slotIndex >= 1 && _scheme != null
            ? _scheme.GetSlotColor(slotIndex, card)
            : PerformSlotColors.Default;
    }

    private float GetHitboxBottomY()
    {
        var creatureNode = _player?.Creature.GetCreatureNode();
        return creatureNode == null
            ? 0f
            : (GetGlobalTransform().AffineInverse() * creatureNode.GetBottomOfHitbox()).Y;
    }

    /// <summary>将命中框角点转换为容器局部坐标。</summary>
    private Vector2 ToContainerPoint(Vector2 hitboxGlobalPoint)
    {
        if (_itemContainer == null) return Vector2.Zero;
        return GetGlobalTransform().AffineInverse() * hitboxGlobalPoint - _itemContainer.Position;
    }

    private float GetHitboxLeftX()
    {
        var creatureNode = _player?.Creature.GetCreatureNode();
        return creatureNode == null ? 0f : ToContainerPoint(creatureNode.Hitbox.GlobalPosition).X;
    }

    private float GetHitboxRightX()
    {
        var creatureNode = _player?.Creature.GetCreatureNode();
        return creatureNode == null
            ? 0f
            : ToContainerPoint(creatureNode.Hitbox.GlobalPosition + new Vector2(creatureNode.Hitbox.Size.X, 0f)).X;
    }

    private float GetHitboxTopY()
    {
        var creatureNode = _player?.Creature.GetCreatureNode();
        return creatureNode == null ? 0f : ToContainerPoint(creatureNode.Hitbox.GlobalPosition).Y;
    }

    private void ApplyDeferredLayout()
    {
        if (_isExiting) return;

        ApplyLayoutImmediately();
        SetHintTarget(GetHintAmount(), true);
        TaskHelper.RunSafely(AnimatePendingSlotEntrances());
    }

    private void OnSecondaryResourceChanged(SecondaryResourceChangedEvent changedEvent)
    {
        if (_scheme is not { UsesSlotHint: true }) return;

        SetHintTarget(GetHintAmount());
    }

    private int GetHintAmount()
    {
        return _scheme is { UsesSlotHint: true } scheme &&
               _player != null &&
               scheme.TryGetHintAmount(_player, out var amount)
            ? amount
            : 0;
    }

    private int NormalizeHintAmount(int amount)
    {
        return Math.Max(0, amount);
    }

    private void SetHintTarget(int amount, bool immediately = false, bool refreshPosition = false)
    {
        _targetHintAmount = NormalizeHintAmount(amount);
        _hintNeedsPositionRefresh |= refreshPosition;

        if (immediately || !_isHintInitialized)
        {
            _isHintInitialized = true;
            _displayedHintAmount = _targetHintAmount;
            _hintNeedsPositionRefresh = false;
            UpdateHintPosition();
            SetHintAlpha(IsHintAmountVisible(_displayedHintAmount) ? 1f : 0f);
            UpdateHintSlotHighlight(true);
            return;
        }

        UpdateHintSlotHighlight();

        if (!_isHintAnimating)
        {
            TaskHelper.RunSafely(AnimateHintToTarget());
        }
    }

    private async Task AnimateHintToTarget()
    {
        if (_hint == null || _isExiting) return;

        _isHintAnimating = true;
        try
        {
            while (!_isExiting &&
                   (_displayedHintAmount != _targetHintAmount || _hintNeedsPositionRefresh))
            {
                if (!IsHintAmountVisible(_targetHintAmount))
                {
                    if (IsHintAmountVisible(_displayedHintAmount))
                    {
                        await TweenHintAlpha(0f);
                    }

                    if (!IsHintAmountVisible(_targetHintAmount))
                    {
                        _displayedHintAmount = _targetHintAmount;
                        _hintNeedsPositionRefresh = false;
                        UpdateHintSlotHighlight();
                        continue;
                    }
                }

                if (IsHintAmountVisible(_displayedHintAmount))
                {
                    await TweenHintAlpha(0f);
                }

                if (_displayedHintAmount != _targetHintAmount)
                {
                    _displayedHintAmount += Math.Sign(_targetHintAmount - _displayedHintAmount);
                }

                _hintNeedsPositionRefresh = false;
                UpdateHintPosition();
                UpdateHintSlotHighlight();

                if (IsHintAmountVisible(_displayedHintAmount))
                {
                    await TweenHintAlpha(1f);
                }
            }
        }
        finally
        {
            _isHintAnimating = false;
        }
    }

    private void UpdateHintPosition()
    {
        if (_hint == null || _itemContainer == null || !IsHintAmountVisible(_displayedHintAmount))
        {
            return;
        }

        var slot = _items[_displayedHintAmount - 1];
        _hint.Position = new Vector2(
            _hintPositionX + slot.Position.X,
            _itemContainer.Position.Y + slot.Position.Y - _hint.Size.Y / 2f + HintVerticalOffset
        );
    }

    /// <summary>
    /// 提示仅指向当前已激活的槽位。祥子的激活槽位恰为 <c>1..Capacity</c>，
    /// 与提示按容量裁剪的既有表现一致。
    /// </summary>
    private bool IsHintAmountVisible(int amount)
    {
        return amount >= 1 && _activeSlots.Contains(amount);
    }

    private void UpdateHintSlotHighlight(bool immediately = false)
    {
        NPerformItem? targetSlot = null;
        if (_displayedHintAmount == _targetHintAmount && IsHintAmountVisible(_displayedHintAmount))
        {
            targetSlot = _items[_displayedHintAmount - 1];
        }

        SetHintHighlightedSlot(targetSlot, immediately);
    }

    private void SetHintHighlightedSlot(NPerformItem? slot, bool immediately = false)
    {
        if (_hintHighlightedSlot == slot)
        {
            if (slot != null && immediately)
            {
                slot.SetHintHighlighted(true, true);
            }

            return;
        }

        if (_hintHighlightedSlot != null && IsInstanceValid(_hintHighlightedSlot))
        {
            _hintHighlightedSlot.SetHintHighlighted(false, immediately);
        }

        _hintHighlightedSlot = slot;
        _hintHighlightedSlot?.SetHintHighlighted(true, immediately);
    }

    private void SetHintAlpha(float alpha)
    {
        if (_hint == null) return;

        var modulate = _hint.Modulate;
        modulate.A = alpha;
        _hint.Modulate = modulate;
    }

    private Task TweenHintAlpha(float alpha)
    {
        if (_hint == null || _isExiting) return Task.CompletedTask;

        CancelHintTween();
        var tween = CreateTween();
        tween.SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(_hint, "modulate:a", alpha, HintFadeDuration)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(alpha > 0f ? Tween.EaseType.Out : Tween.EaseType.In);

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runningTween = new RunningTween(tween, completion);
        _hintTween = runningTween;
        tween.Finished += () =>
        {
            completion.TrySetResult();
            if (_hintTween == runningTween)
            {
                _hintTween = null;
            }
        };

        return completion.Task;
    }

    private void CancelLayoutTween()
    {
        if (_layoutTween == null) return;

        _layoutTween.Tween.Kill();
        _layoutTween.Completion.TrySetResult();
        _layoutTween = null;
    }

    private void CancelSlotEntranceTween(bool complete)
    {
        if (_slotEntranceTween != null)
        {
            _slotEntranceTween.Tween.Kill();
            _slotEntranceTween.Completion.TrySetResult();
            _slotEntranceTween = null;
        }

        if (complete)
        {
            CompleteSlotEntrances();
        }
        else
        {
            _enteringSlots.Clear();
            _pendingEntranceSlots.Clear();
        }
    }

    private void CancelHintTween()
    {
        if (_hintTween == null) return;

        _hintTween.Tween.Kill();
        _hintTween.Completion.TrySetResult();
        _hintTween = null;
    }
}