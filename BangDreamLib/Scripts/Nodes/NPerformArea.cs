using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Mechanics.Perform;
using BangDreamLib.Scripts.Nodes.SubNode;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;

namespace BangDreamLib.Scripts.Nodes;

/// <summary>
/// 歌单演奏区域节点。槽位数量与布局拓扑由角色的演奏方案提供，本节点只负责
/// 像素换算、可见性与缓动。
/// </summary>
public partial class NPerformArea : Control
{
    private const float ItemHeight = 64f;
    private const float ItemSpacing = 70f;
    private const float ItemApproachSpacing = 25f;

    /// <summary>四角槽位与命中框边界之间的水平间距。</summary>
    private const float SlotHitboxGap = 10f;

    private const float LayoutDuration = 0.25f;
    private const float SlotEntranceDuration = 0.3f;
    private const float SlotEntranceDelay = 0.04f;
    private const float SlotEntranceHorizontalOffset = 56f;
    private const float HintFadeDuration = 0.12f;
    private const float HintVerticalOffset = 5f;

    /// <summary>手动选择时槽位命中框额外放宽的边距（像素）。</summary>
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
    private Player? _player;
    private IPerformScheme? _scheme;

    private readonly List<NPerformItem> _items = [];
    private readonly List<NPerformItem> _pendingEntranceSlots = [];
    private readonly List<SlotEntranceState> _enteringSlots = [];
    private readonly HashSet<int> _activeSlots = [];
    private readonly List<int> _newlyActiveSlots = [];
    private readonly Dictionary<CardModel, Vector2> _lastCardSlotCenters = [];
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

    /// <summary>
    /// 绑定角色的演奏方案。必须在 <see cref="SyncSlots" /> 之前调用。
    /// </summary>
    public void SubmitScheme(IPerformScheme scheme)
    {
        _scheme = scheme;
    }

    public override void _Ready()
    {
        _itemContainer = GetNode<Control>("%ItemContainer");
        _hint = GetNode<TextureRect>("%Hint");
        _hintPositionX = _hint.Position.X;
        _items.AddRange(_itemContainer.GetChildren().OfType<NPerformItem>());
        foreach (var item in _items)
        {
            item.AttachToArea(this);
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

        SecondaryResourceStateStore.Get(_player).Changed += OnSecondaryResourceChanged;
    }

    /// <summary>该演奏区域当前是否处于手动落位选择态。</summary>
    public bool IsSlotSelectionActive => _slotSelectionCard != null;

    /// <summary>正在等待玩家指定落位的卡牌；未处于选择态时为 <see langword="null" />。</summary>
    public CardModel? SlotSelectionCard => _slotSelectionCard;

    /// <summary>当前指向的槽位索引；未指向任何候选槽位时为 0。</summary>
    public int HoveredSlotIndex => _slotSelectionHoveredSlot;

    /// <summary>
    /// 进入手动落位选择态：高亮候选槽位，并在后续帧中按鼠标位置更新指向。
    /// </summary>
    /// <param name="cardModel">等待指定落位的卡牌。</param>
    /// <param name="mode">选择模式：指定槽位或指定分组。</param>
    /// <param name="candidates">候选槽位及其所属分组，按槽位索引升序。</param>
    public void BeginSlotSelection(
        CardModel cardModel,
        PerformSlotSelectionMode mode,
        IReadOnlyList<PerformSlotCandidate> candidates)
    {
        EndSlotSelection();
        if (mode == PerformSlotSelectionMode.None || candidates.Count == 0) return;

        _slotSelectionCard = cardModel;
        _slotSelectionMode = mode;
        _slotSelectionCandidates.AddRange(candidates);
        SetProcess(true);
        UpdateSlotSelection(force: true);
    }

    /// <summary>
    /// 立即按当前鼠标位置刷新一次指向（用于松手瞬间等需要精确判定的时点）。
    /// </summary>
    public void RefreshSlotSelection()
    {
        if (_slotSelectionCard == null) return;

        UpdateSlotSelection(force: true);
    }

    /// <summary>
    /// 取当前指向的落位结果并结束选择。未指向任何候选槽位时返回空请求。
    /// </summary>
    public PerformEnqueueRequest TryConsumeSlotSelection()
    {
        var request = BuildSelectedRequest();
        EndSlotSelection();
        return request;
    }

    /// <summary>结束选择态并复位槽位高亮。</summary>
    public void EndSlotSelection()
    {
        _slotSelectionCard = null;
        _slotSelectionMode = PerformSlotSelectionMode.None;
        _slotSelectionCandidates.Clear();
        _slotSelectionHoveredSlot = 0;
        _slotSelectionPointerSlot = 0;
        SetProcess(false);
        ResetSlotSelectionVisuals();
        PerformTargetingArrow.End();
    }

    public override void _Process(double delta)
    {
        if (_slotSelectionCard == null) return;

        UpdateSlotSelection(force: false);
    }

    /// <summary>按鼠标位置刷新指向的候选槽位；<paramref name="force" /> 用于进入选择态时立即建立高亮。</summary>
    private void UpdateSlotSelection(bool force)
    {
        RefreshHoveredSlot(force);
    }

    /// <summary>
    /// 鼠标指针进入某个槽位。以 Godot 自身的鼠标拾取为准（不受缩放/镜像/父级变换影响），
    /// 手算命中作为其失效时的兜底。
    /// </summary>
    internal void NotifySlotPointerEntered(NPerformItem item)
    {
        if (_slotSelectionCard == null) return;

        var slotIndex = _items.IndexOf(item) + 1;
        if (!IsCandidateSlot(slotIndex)) return;

        _slotSelectionPointerSlot = slotIndex;
        RefreshHoveredSlot(force: true);
    }

    /// <summary>鼠标指针离开某个槽位。</summary>
    internal void NotifySlotPointerExited(NPerformItem item)
    {
        if (_slotSelectionCard == null) return;
        if (_items.IndexOf(item) + 1 != _slotSelectionPointerSlot) return;

        _slotSelectionPointerSlot = 0;
        RefreshHoveredSlot(force: true);
    }

    /// <summary>
    /// 重新确定指向的槽位：鼠标拾取优先，其次手算命中；两者都未命中时再用视口坐标兜底判定。
    /// </summary>
    private void RefreshHoveredSlot(bool force = false)
    {
        var hovered = _slotSelectionPointerSlot;
        if (hovered <= 0)
        {
            hovered = HitTestCandidateSlot(GetGlobalMousePosition());
        }

        if (hovered <= 0)
        {
            // 兜底：若演奏区节点与鼠标的坐标空间不一致（父级缩放/相机位移等），用视口坐标再判一次。
            var viewportMouse = GetViewport().GetMousePosition();
            if (!viewportMouse.IsEqualApprox(GetGlobalMousePosition()))
            {
                hovered = HitTestCandidateSlot(viewportMouse);
            }
        }

        if (!force && hovered == _slotSelectionHoveredSlot) return;

        _slotSelectionHoveredSlot = hovered;
        ApplySlotSelectionVisuals();

        // 复用原版目标指示箭头：指向候选槽位时高亮，离开时恢复。
        PerformTargetingArrow.SetHighlighted(hovered > 0);
    }

    private bool IsCandidateSlot(int slotIndex)
    {
        return slotIndex >= 1 &&
               _slotSelectionCandidates.Any(candidate => candidate.SlotIndex == slotIndex);
    }

    /// <summary>选择态诊断文本：鼠标位置、指针/手算指向的槽位、各候选槽位的命中几何。</summary>
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

    /// <summary>后绘制的槽位优先命中；非激活槽位不参与。</summary>
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

    /// <summary>
    /// 刷新高亮：指定分组模式下，指向的槽位会让同组全部候选槽位一并高亮（其余只有候选高亮）。
    /// </summary>
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
        _slotSelectionCard = null;
        _slotSelectionMode = PerformSlotSelectionMode.None;
        _slotSelectionCandidates.Clear();
        _slotSelectionHoveredSlot = 0;
        _slotSelectionPointerSlot = 0;
        SetProcess(false);
        PerformTargetingArrow.End();

        if (_player != null && SecondaryResourceStateStore.TryGet(_player, out var resourceState))
        {
            resourceState.Changed -= OnSecondaryResourceChanged;
        }

        CancelLayoutTween();
        CancelSlotEntranceTween(false);
        CancelHintTween();
        SetHintHighlightedSlot(null, true);
        _lastCardSlotCenters.Clear();
        _pendingArrivalBounces.Clear();
    }

    /// <summary>
    /// 同步演奏区域的总槽位数与已激活槽位集合。总槽位数决定节点数量，
    /// 激活集合决定哪些槽位可见并参与布局。
    /// </summary>
    public void SyncSlots(int totalSlotCount, IReadOnlyCollection<int> activeSlots)
    {
        var nextSlotCount = Math.Max(0, totalSlotCount);
        var nextActiveSlots = activeSlots.ToHashSet();
        var changed = _slotCount != nextSlotCount || !_activeSlots.SetEquals(nextActiveSlots);

        _slotCount = nextSlotCount;
        Visible = _slotCount > 0 && nextActiveSlots.Count > 0;

        // 场景预置的槽位不经过 EnsureSlotCount 的新建路径，镜像状态需在此每次重算。
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
        _lastCardSlotCenters[cardModel] = slot.GetSlotGlobalCenter();
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

        _lastCardSlotCenters[cardModel] = item.GetSlotGlobalCenter();

        if (item.Context != null)
        {
            item.Context.Slot = null;
        }

        item.Context = null;
        item.Model = null;
    }

    public bool TryGetCardSlotCenter(CardModel cardModel, out Vector2 center)
    {
        var activeSlot = _items.FirstOrDefault(item => item.Model == cardModel);
        if (activeSlot != null)
        {
            center = activeSlot.GetSlotGlobalCenter();
            _lastCardSlotCenters[cardModel] = center;
            return true;
        }

        if (_player == cardModel.Owner)
        {
            var slotIndex = _player.AttachedData().PerformManager.GetExpectedSlotIndex(cardModel);
            if (slotIndex >= 1 && slotIndex <= _items.Count && _activeSlots.Contains(slotIndex))
            {
                center = _items[slotIndex - 1].GetSlotGlobalCenter();
                _lastCardSlotCenters[cardModel] = center;
                return true;
            }
        }

        return _lastCardSlotCenters.TryGetValue(cardModel, out center);
    }

    /// <summary>
    /// 逻辑层批量调整槽位后，通知现有 item 平滑移动到新的顺序。
    /// </summary>
    public void RefreshItemLayout()
    {
        ReassignItemsToSlots();
        ApplyItemVisibility();
        TaskHelper.RunSafely(AnimateLayout());
    }

    private async Task ApplySlotSync()
    {
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
        };

        return completion.Task;
    }

    private void ApplyLayoutImmediately()
    {
        if (_itemContainer == null) return;

        ApplyItemVisibility();
        for (var slotIndex = 1; slotIndex <= _items.Count; slotIndex++)
        {
            if (!_activeSlots.Contains(slotIndex)) continue;
            _items[slotIndex - 1].Position = GetItemPosition(slotIndex);
        }

        UpdateHintPosition();
    }

    private void ApplyItemVisibility()
    {
        for (var index = 0; index < _items.Count; index++)
        {
            _items[index].Visible = _activeSlots.Contains(index + 1);
        }
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
            slot.Visible = false;
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
            // 自命中框外侧滑入：左侧槽位自左、右侧槽位自右。
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

    /// <summary>
    /// 计算全局槽位的像素位置。拓扑来自角色的演奏方案，像素换算留在本节点。
    /// </summary>
    /// <remarks>
    /// 返回的是槽位节点的原点。物品原点（未镜像时）位于物品右边缘中点、物品向左延展，
    /// 因此命中框左侧的槽位以右边缘贴住命中框左边界；右侧的槽位经水平镜像后原点位于
    /// 物品左边缘中点、以左边缘贴住命中框右边界。两侧因此呈镜像对称，且
    /// <see cref="SlotHitboxGap" /> 间距与物品的自适应宽度无关。
    /// 四角槽位不使用水平靠近偏移，否则会破坏该间距。
    /// </remarks>
    private Vector2 GetItemPosition(int slotIndex)
    {
        if (_itemContainer == null) return Vector2.Zero;

        var anchor = _scheme?.GetSlotAnchor(slotIndex) ??
                     new PerformSlotAnchor(PerformSlotCorner.BottomCenter, slotIndex - 1);
        var stack = anchor.Offset * ItemSpacing * _itemScale;
        var approach = anchor.Offset * ItemApproachSpacing * _itemScale;
        var bottom = GetHitboxBottomY() - _itemContainer.Position.Y;

        return anchor.Corner switch
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
    }

    private PerformSlotCorner GetCorner(int slotIndex)
    {
        return (_scheme?.GetSlotAnchor(slotIndex) ??
                new PerformSlotAnchor(PerformSlotCorner.BottomCenter, slotIndex - 1)).Corner;
    }

    /// <summary>命中框右侧的槽位以水平镜像排布，使其与左侧槽位互为镜像。</summary>
    private static bool IsMirroredCorner(PerformSlotCorner corner)
    {
        return corner is PerformSlotCorner.TopRight or PerformSlotCorner.BottomRight;
    }

    private void ApplySlotScale(NPerformItem item, int slotIndex)
    {
        item.ApplyLayoutScale(_itemScale, IsMirroredCorner(GetCorner(slotIndex)));
    }

    /// <summary>
    /// 取指定槽位的显示颜色，配色依据由角色的演奏方案决定。
    /// </summary>
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

    /// <summary>
    /// 把命中框角点的全局坐标换算到 <see cref="_itemContainer" /> 的本地坐标。
    /// </summary>
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