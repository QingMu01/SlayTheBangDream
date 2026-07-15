using BangDreamLib.Scripts.Extensions;
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

public partial class NPerformArea : Control
{
    private const int MaxCapacity = 7;
    private const float ItemHeight = 64f;
    private const float ItemSpacing = 70f;
    private const float ItemApproachSpacing = 25f;

    private const float LayoutDuration = 0.25f;
    private const float SlotEntranceDuration = 0.3f;
    private const float SlotEntranceDelay = 0.04f;
    private const float SlotEntranceHorizontalOffset = 56f;
    private const float HintFadeDuration = 0.12f;
    private const float HintVerticalOffset = 5f;

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

    private readonly List<NPerformItem> _items = [];
    private readonly List<NPerformItem> _pendingEntranceSlots = [];
    private readonly List<SlotEntranceState> _enteringSlots = [];
    private readonly Dictionary<CardModel, Vector2> _lastCardSlotCenters = [];
    private readonly HashSet<CardModel> _pendingArrivalBounces = [];

    private RunningTween? _layoutTween;
    private RunningTween? _slotEntranceTween;
    private RunningTween? _hintTween;
    private int _capacity;
    private int _displayedHintAmount;
    private int _targetHintAmount;
    private NPerformItem? _hintHighlightedSlot;
    private float _hintPositionX;
    private bool _isHintInitialized;
    private bool _isHintAnimating;
    private bool _hintNeedsPositionRefresh;
    private bool _isExiting;

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

    public override void _Ready()
    {
        _itemContainer = GetNode<Control>("%ItemContainer");
        _hint = GetNode<TextureRect>("%Hint");
        _hintPositionX = _hint.Position.X;
        _items.AddRange(_itemContainer.GetChildren().OfType<NPerformItem>());

        Visible = _capacity > 0;
        EnsureSlotCount();
        ApplyItemScale();
        ApplyLayoutImmediately();
        SetHintTarget(GetLingeredResourceAmount(), true);
        Callable.From(ApplyDeferredLayout).CallDeferred();
    }

    public void SubmitChanged()
    {
        if (_player != null)
        {
            SecondaryResourceStateStore.Get(_player).Changed += OnSecondaryResourceChanged;
        }
    }

    public override void _ExitTree()
    {
        _isExiting = true;
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

    public void SetCapacity(int capacity)
    {
        var normalizedCapacity = Math.Clamp(capacity, 0, MaxCapacity);
        Visible = normalizedCapacity > 0;
        if (_capacity == normalizedCapacity) return;

        var previousCapacity = _capacity;
        _capacity = normalizedCapacity;
        TaskHelper.RunSafely(ApplyCapacityChanged(previousCapacity));
    }

    public void AddItem(CardModel cardModel, PerformContext context)
    {
        if (_itemContainer == null || _items.Any(item => item.Model == cardModel)) return;
        if (context.SlotIndex < 1 || context.SlotIndex > _items.Count) return;

        var slot = _items[context.SlotIndex - 1];
        if (slot.Model != null) return;

        slot.PrepareCardArrival();
        slot.Model = cardModel;
        slot.Context = context;
        context.Slot = slot;
        _lastCardSlotCenters[cardModel] = slot.GetSlotGlobalCenter();
        if (_pendingArrivalBounces.Remove(cardModel))
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
            if (slotIndex >= 1 && slotIndex <= _items.Count)
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
        TaskHelper.RunSafely(AnimateLayout());
    }

    private async Task ApplyCapacityChanged(int previousCapacity)
    {
        EnsureSlotCount();
        await AnimateCapacityChanged(previousCapacity, _capacity);
        await AnimateLayout();
        SetHintTarget(GetLingeredResourceAmount(), false, true);
    }

    private Task AnimateCapacityChanged(int previousCapacity, int capacity)
    {
        return capacity > previousCapacity ? AnimatePendingSlotEntrances() : Task.CompletedTask;
    }

    private Task AnimateLayout()
    {
        if (_itemContainer == null) return Task.CompletedTask;

        CancelSlotEntranceTween(true);
        CancelLayoutTween();
        var orderedItems = GetOrderedItems();
        if (orderedItems.Count == 0) return Task.CompletedTask;

        var tween = CreateTween();
        tween.SetParallel();
        tween.SetPauseMode(Tween.TweenPauseMode.Process);

        for (var index = 0; index < orderedItems.Count; index++)
        {
            var item = orderedItems[index];
            if (!item.IsInsideTree()) continue;

            tween.TweenProperty(item, "position", GetItemPosition(index), LayoutDuration)
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

        var orderedItems = GetOrderedItems();
        for (var index = 0; index < orderedItems.Count; index++)
        {
            orderedItems[index].Position = GetItemPosition(index);
        }

        UpdateHintPosition();
    }

    private void EnsureSlotCount()
    {
        if (_itemContainer == null) return;

        while (_items.Count < _capacity)
        {
            var slot = NPerformItem.Create(this);
            slot.Position = GetItemPosition(_items.Count);
            slot.Scale = Vector2.One * _itemScale;
            _items.Add(slot);
            _itemContainer.AddChild(slot);
            _pendingEntranceSlots.Add(slot);
        }

        while (_items.Count > _capacity)
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
            var index = _items.IndexOf(slot);
            if (index < 0 || !slot.IsInsideTree()) continue;

            var targetPosition = GetItemPosition(index);
            var targetAlpha = slot.Modulate.A;
            _enteringSlots.Add(new SlotEntranceState(slot, targetPosition, targetAlpha));
            slot.Position = targetPosition + Vector2.Left * SlotEntranceHorizontalOffset;
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
        var scale = Vector2.One * _itemScale;
        foreach (var item in _items)
        {
            item.Scale = scale;
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

    private List<NPerformItem> GetOrderedItems()
    {
        return _items.ToList();
    }

    private Vector2 GetItemPosition(int index)
    {
        if (_itemContainer == null) return Vector2.Zero;

        var bottom = GetHitboxBottomY() - _itemContainer.Position.Y;
        var distanceFromBottom = index * ItemSpacing * _itemScale;
        var distanceTowardsPlayer = index * ItemApproachSpacing * _itemScale;
        return new Vector2(distanceTowardsPlayer, bottom - ItemHeight / 2f - distanceFromBottom);
    }

    private float GetHitboxBottomY()
    {
        var creatureNode = _player?.Creature.GetCreatureNode();
        return creatureNode == null
            ? 0f
            : (GetGlobalTransform().AffineInverse() * creatureNode.GetBottomOfHitbox()).Y;
    }

    private void ApplyDeferredLayout()
    {
        if (_isExiting) return;

        ApplyLayoutImmediately();
        SetHintTarget(GetLingeredResourceAmount(), true);
        TaskHelper.RunSafely(AnimatePendingSlotEntrances());
    }

    private void OnSecondaryResourceChanged(SecondaryResourceChangedEvent changedEvent)
    {
        if (!changedEvent.Definition.Id.Equals(BangDreamConst.LingeredResource)) return;

        SetHintTarget(changedEvent.NewAmount);
    }

    private int GetLingeredResourceAmount()
    {
        return _player == null ? 0 : SecondaryResourceCmd.Get(_player, BangDreamConst.LingeredResource);
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
        if (_hint == null || _itemContainer == null || _displayedHintAmount < 1 ||
            _displayedHintAmount > _items.Count)
        {
            return;
        }

        var slot = _items[_displayedHintAmount - 1];
        _hint.Position = new Vector2(
            _hintPositionX + slot.Position.X,
            _itemContainer.Position.Y + slot.Position.Y - _hint.Size.Y / 2f + HintVerticalOffset
        );
    }

    private bool IsHintAmountVisible(int amount)
    {
        return amount >= 1 && amount <= _items.Count;
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
