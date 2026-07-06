using System.Runtime.CompilerServices;
using BangDreamLib.Scripts.Features;
using BangDreamLib.Scripts.Nodes.SubNode;
using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace BangDreamLib.Scripts.Nodes;

public partial class NPerformArea : Control
{
    private const float ItemStep = -96f;

    private bool _isLocal;
    private Control? _itemContainer;
    private PerformManager? _manager;

    private readonly List<NPerformItem> _items = [];
    private readonly ConditionalWeakTable<NPerformItem, Tween> _itemAnims = new();

    private TaskCompletionSource? _layoutCompletion;
    private Tween? _layoutTween;

    public static NPerformArea Create(PerformManager manager, Vector2 offset)
    {
        var area = PreloadKey.PerformanceArea.GetScene().Instantiate<NPerformArea>();
        area._manager = manager;
        area._isLocal = LocalContext.IsMe(manager.Player);
        area.Position -= offset;
        return area;
    }

    public override void _Ready()
    {
        _itemContainer = GetNode<Control>((NodePath)"%PerformanceItems");
        if (!_isLocal) Modulate = new Color(0.5f, 0.5f, 0.5f);

        if (_manager != null)
            _ = OnCapacityChanged(_manager.Capacity);
    }

    public override void _EnterTree()
    {
        if (_manager != null)
        {
            _manager.CardEnteredPerformance += OnCardEnteredPerformance;
            _manager.CardLeftPerformance += OnCardLeftPerformance;
            _manager.CapacityChanged += OnCapacityChanged;
        }
    }

    public override void _ExitTree()
    {
        if (_manager != null)
        {
            _manager.CardEnteredPerformance -= OnCardEnteredPerformance;
            _manager.CardLeftPerformance -= OnCardLeftPerformance;
            _manager.CapacityChanged -= OnCapacityChanged;
        }
    }

    public Vector2? GetPilePost(CardModel? card)
    {
        if (_items.Count == 0 || card == null)
        {
            return _itemContainer?.GlobalPosition;
        }

        var item = _items.FirstOrDefault(performanceItem => performanceItem.Model == card);
        if (item != null)
        {
            return item.GlobalPosition;
        }

        var emptyItem = _items.FirstOrDefault(performanceItem => performanceItem.Model == null);
        if (emptyItem != null)
        {
            return GetGlobalItemPosition(_items.IndexOf(emptyItem));
        }

        return GetGlobalItemPosition(_items.Count);
    }

    private async Task OnCardEnteredPerformance(CardModel card)
    {
        var emptyItem = _items.FirstOrDefault(item => item.Model == null);
        if (emptyItem != null)
        {
            emptyItem.Model = card;
        }
        else
        {
            AddItem(NPerformItem.Create(_isLocal, card));
        }

        await AnimateLayout();
    }

    private async Task OnCardLeftPerformance(CardModel card)
    {
        var item = _items.FirstOrDefault(item => item.Model == card);
        if (item != null)
        {
            RemoveItem(item);
        }

        await OnCapacityChanged(_manager?.Capacity ?? 0);

        await AnimateLayout();
    }

    private async Task OnCapacityChanged(int newCapacity)
    {
        var diff = newCapacity - _items.Count;
        if (diff > 0)
        {
            for (var i = 0; i < diff; i++)
            {
                AddItem(NPerformItem.Create(_isLocal));
            }
        }
        else if (diff < 0)
        {
            for (var i = 0; i < -diff; i++)
            {
                var toRemove = _items.LastOrDefault();
                if (toRemove != null)
                {
                    RemoveItem(toRemove);
                }
            }
        }

        await AnimateLayout();
    }

    private Task AnimateLayout()
    {
        _layoutTween?.Kill();
        _layoutCompletion?.TrySetResult();
        _layoutTween = null;
        _layoutCompletion = null;

        var tween = CreateTween();
        tween.SetParallel();
        tween.SetPauseMode(Tween.TweenPauseMode.Stop);

        var hasTween = false;
        for (var i = 0; i < _items.Count; i++)
        {
            var currentItem = _items[i];
            var newPos = GetItemPosition(i);
            if (Math.Abs(currentItem.Position.DistanceTo(newPos)) > 5f)
            {
                hasTween = true;
                tween.TweenProperty(currentItem, "position", newPos, 0.25f)
                    .SetDelay(0.5f)
                    .SetTrans(Tween.TransitionType.Quad)
                    .SetEase(Tween.EaseType.InOut);
            }
        }

        if (!hasTween)
        {
            tween.Kill();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _layoutTween = tween;
        _layoutCompletion = completion;
        tween.Finished += () =>
        {
            completion.TrySetResult();
            if (_layoutTween != tween) return;

            _layoutTween = null;
            _layoutCompletion = null;
        };
        tween.SetPauseMode(Tween.TweenPauseMode.Process);
        return completion.Task;
    }

    private void AddItem(NPerformItem item)
    {
        if (item.IsValid())
        {
            item.Position = GetItemPosition(_items.Count);

            var tween = CreateTween();
            TrackTween(item, tween);
            item.Scale = Vector2.Zero;
            tween.TweenProperty(item, "scale", Vector2.One, 1f)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out)
                .Set("backStrength", 8f);
            _items.Add(item);
            _itemContainer?.AddChildSafely(item);
            tween.Finished += () => { _itemAnims.Remove(item); };
        }
    }

    private void RemoveItem(NPerformItem item)
    {
        if (item.IsValid())
        {
            var tween = CreateTween();
            TrackTween(item, tween);
            item.Scale = Vector2.One;
            tween.TweenProperty(item, "scale", Vector2.Zero, 1f)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.In)
                .Set("backStrength", 8f);
            _items.Remove(item);
            tween.Finished += () =>
            {
                _itemAnims.Remove(item);
                item.QueueFreeSafely();
            };
        }
    }

    private static Vector2 GetItemPosition(int index)
    {
        return new Vector2(index >= 5 ? ItemStep : 0, index % 5 * ItemStep);
    }

    private Vector2? GetGlobalItemPosition(int index)
    {
        return _itemContainer?.GetGlobalTransform() * GetItemPosition(index);
    }

    private void TrackTween(NPerformItem item, Tween tween)
    {
        if (_itemAnims.TryGetValue(item, out var existingTween))
        {
            existingTween.Kill();
            _itemAnims.Remove(item);
        }

        _itemAnims.Add(item, tween);
    }
}