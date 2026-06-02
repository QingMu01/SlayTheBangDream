using System.Runtime.CompilerServices;
using BangDreamLib.Scripts.Features;
using BangDreamLib.Scripts.Nodes.SubNode;
using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamLib.Scripts.Nodes;

public partial class NPerformanceArea : Control
{
    private const float ItemStep = -96f;

    private bool _isLocal;
    private Control? _itemContainer;
    private PerformanceManager? _manager;

    private readonly List<NPerformanceItem> _items = [];
    private readonly ConditionalWeakTable<NPerformanceItem, Tween> _itemAnims = new();

    private bool _flipFlag;

    public static NPerformanceArea Create(PerformanceManager manager, Vector2 offset)
    {
        var area = PreloadKey.PerformanceArea.GetScene().Instantiate<NPerformanceArea>();
        area._manager = manager;
        area._isLocal = LocalContext.IsMe(manager.Player);
        area.Position -= offset;
        return area;
    }

    public Vector2 GetSlotPosWithAutoFlip()
    {
        int? currentIndex = null;
        var count = _manager?.PerformancePile.Cards.Count ?? 0;
        var capacity = _manager?.Capacity ?? 0;

        if (count == capacity)
        {
            if (_flipFlag)
            {
                _flipFlag = !_flipFlag;
                currentIndex = 0;
            }
        }

        currentIndex ??= Math.Max(0, count - 1);

        return (_itemContainer?.GlobalPosition ?? Vector2.Zero) + new Vector2(0, currentIndex.Value * ItemStep);
    }

    public override void _Ready()
    {
        _itemContainer = GetNode<Control>((NodePath)"%PerformanceItems");
        if (!_isLocal) Modulate = new Color(0.5f, 0.5f, 0.5f);

        OnRefreshLayout();
    }

    public override void _EnterTree()
    {
        if (_manager != null)
            _manager.RefreshLayout += OnRefreshLayout;
    }

    public override void _ExitTree()
    {
        if (_manager != null)
            _manager.RefreshLayout -= OnRefreshLayout;
    }

    private void OnRefreshLayout()
    {
        UpdateCapacityChanged();

        UpdateItemsChanged();

        var hasTween = false;
        var tween = CreateTween();
        tween.SetParallel();
        tween.SetPauseMode(Tween.TweenPauseMode.Stop);
        for (var i = 0; i < _items.Count; i++)
        {
            var currentItem = _items[i];
            if (Math.Abs(currentItem.Position.Y - i * ItemStep) > 0.5)
            {
                hasTween = true;
                tween.TweenProperty(currentItem, "position:y", i * ItemStep, 0.25f)
                    .SetDelay(0.5f)
                    .SetTrans(Tween.TransitionType.Quad)
                    .SetEase(Tween.EaseType.InOut);
            }
        }

        if (!hasTween)
        {
            tween.Kill();
            return;
        }

        tween.SetPauseMode(Tween.TweenPauseMode.Process);
    }

    private void AddItem(NPerformanceItem item)
    {
        item.Position = new Vector2(0, _items.Count * ItemStep);

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

    private void RemoveItem(NPerformanceItem item)
    {
        var tween = CreateTween();
        TrackTween(item, tween);
        item.Scale = Vector2.One;
        tween.TweenProperty(item, "scale", Vector2.Zero, 1f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.In)
            .Set("backStrength", 5f);
        _items.Remove(item);
        tween.Finished += () =>
        {
            _itemAnims.Remove(item);
            item.QueueFreeSafely();
        };
    }

    private void UpdateItemsChanged()
    {
        if (_manager == null)
        {
            BangDreamLibCore.Logger.Error("PerformanceArea: PerformanceManager is null.");
            return;
        }

        var cardPileCards = _manager.PerformancePile.Cards;

        // 同步已添加的卡牌
        foreach (var card in cardPileCards)
        {
            if (_items.Any(item => item.Model == card))
            {
                continue;
            }

            var performanceItem = _items.FirstOrDefault(item => item.Model == null);
            if (performanceItem != null)
            {
                performanceItem.Model = card;
            }
            else
            {
                AddItem(NPerformanceItem.Create(card.Owner == _manager.Player, card));
            }
        }

        // 同步已被移除演奏区的卡牌
        foreach (var item in _items.Where(item => item.Model != null && !cardPileCards.Contains(item.Model)).ToList())
        {
            RemoveItem(item);
        }
    }

    private void UpdateCapacityChanged()
    {
        if (_manager == null)
        {
            BangDreamLibCore.Logger.Error("PerformanceArea: PerformanceManager is null.");
            return;
        }

        var diffCapacity = _manager.Capacity - _items.Count;
        if (diffCapacity > 0)
        {
            for (var i = 0; i < diffCapacity; i++)
            {
                AddItem(NPerformanceItem.Create(_isLocal));
            }
        }
        else if (diffCapacity < 0)
        {
            for (var i = 0; i < -diffCapacity; i++)
            {
                var performanceItem = _items.FirstOrDefault(item => item.Model != null) ?? _items.LastOrDefault();
                if (performanceItem != null)
                {
                    RemoveItem(performanceItem);
                }
            }
        }
    }

    private void TrackTween(NPerformanceItem item, Tween tween)
    {
        if (_itemAnims.TryGetValue(item, out var existingTween))
        {
            existingTween.Kill();
            _itemAnims.Remove(item);
        }

        _itemAnims.Add(item, tween);
    }
}