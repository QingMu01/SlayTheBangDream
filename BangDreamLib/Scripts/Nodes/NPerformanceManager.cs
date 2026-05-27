using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using NPerformanceItem = BangDreamLib.Scripts.Nodes.SubNode.NPerformanceItem;

namespace BangDreamLib.Scripts.Nodes;

public partial class NPerformanceManager : Control
{
    private static readonly LocString EmptyThink = new("combat_messages", "BANG_DREAM_LIB_FILL_PERFORMANCE_PILE.empty");
    private static readonly LocString MaxSizeThink = new("combat_messages", "BANG_DREAM_LIB_MAX_SIZE_PERFORMANCE_PILE.filled");

    private Control _ItemContainer;
    private NCreature _creatureNode;
    private bool _isLocal;
    private bool _flipFlag;

    private readonly List<NPerformanceItem> _items = [];

    private const float ItemStep = -96f;
    private const int MaxCapacity = 7;
    public Player Player { get; private set; }
    public CardPile PerformancePile { get; private set; }
    public int Capacity { get; private set; }

    public int ItemCount => _items.Count(item => item.Model != null);

    public IReadOnlyList<NPerformanceItem> Items => _items.AsReadOnly();

    public static NPerformanceManager Create(NCreature creature, bool isLocal)
    {
        ArgumentNullException.ThrowIfNull(creature.Entity.Player);

        var manager = PreloadKey.PerformanceManager.GetScene().Instantiate<NPerformanceManager>();
        manager._isLocal = isLocal;
        manager._creatureNode = creature;
        manager.PerformancePile = BangDreamConst.PilePerformance.GetPile(creature.Entity.Player);
        manager.Player = creature.Entity.Player;
        manager.Position -= new Vector2(creature.Hitbox.Size.X / 2f + 48f, 48f);
        if (creature.Entity.Player.Character is IPerformanceCharacter character)
        {
            manager.Capacity = character.GetDefaultCapacity;
        }
        else
        {
            manager.Capacity = 0;
        }

        return manager;
    }

    public override void _Ready()
    {
        _ItemContainer = GetNode<Control>((NodePath)"%PerformanceItems");
        if (!_isLocal)
        {
            Modulate = new Color(0.5f, 0.5f, 0.5f);
        }

        UpdateItems();
        AnimateToFinalLayout();
    }

    public override void _EnterTree()
    {
        base._EnterTree();
        PerformancePile.CardAdded += OnCardAdded;
        PerformancePile.CardRemoved += OnCardRemoved;
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        PerformancePile.CardAdded -= OnCardAdded;
        PerformancePile.CardRemoved -= OnCardRemoved;
    }

    public Vector2 GetSlotPosWithAutoFlip()
    {
        if (ItemCount == Capacity)
        {
            if (_flipFlag)
            {
                _flipFlag = !_flipFlag;
                return GetSlotPos(0);
            }

            _flipFlag = !_flipFlag;
        }

        return GetSlotPos(ItemCount);
    }

    public void AddCapacity(int amount, bool force = false)
    {
        if (amount <= 0) return;
        Capacity += amount;
        if (Capacity > MaxCapacity && !force)
        {
            Capacity = MaxCapacity;
            ThinkCmd.Play(MaxSizeThink, Player.Creature, 1.5d);
        }

        if (!UpdateItems())
            AnimateToFinalLayout();
    }

    public void ReduceCapacity(int amount)
    {
        if (amount <= 0) return;
        Capacity = Math.Max(0, Capacity - amount);
        if (!UpdateItems())
            AnimateToFinalLayout();
    }

    public async Task RemoveItem(CardModel cardModel)
    {
        if (PerformancePile.Cards.Contains(cardModel))
        {
            await HandleCardRemoval(cardModel);
        }
    }

    public async Task AddItem(CardModel cardModel)
    {
        if (PerformancePile.Cards.Contains(cardModel))
        {
            return;
        }

        await CardPileCmd.Add(cardModel, PerformancePile);
    }

    private async void OnCardAdded(CardModel cardModel)
    {
        if (!CombatManager.Instance.IsInProgress)
        {
            return;
        }

        var isInstant = cardModel is IPerformanceCard { IsInstant: true };
        NPerformanceItem? slot = null;
        if (!isInstant && Capacity == 0)
        {
            ThinkCmd.Play(EmptyThink, Player.Creature, 1.5d);
            await HandleCardRemoval(cardModel);
            return;
        }

        await Cmd.CustomScaledWait(0.5f, 1.0f);
        if (cardModel is IPerformanceCard performanceCard)
        {
            await performanceCard.OnStartPerformance(new HookPlayerChoiceContext(cardModel, cardModel.Owner.NetId,
                cardModel.CombatState!, GameActionType.Combat));
        }

        await BangDreamHook.OnCardEnterPerformanceArea(cardModel.CombatState!, cardModel);

        foreach (var item in _items.Where(item => item.Model == null))
        {
            item.Model = cardModel;
            slot = item;
            break;
        }

        if (slot == null)
        {
            slot = NPerformanceItem.Create(LocalContext.IsMe(Player), cardModel);
            slot.Scale = Vector2.Zero;
            _ItemContainer.AddChildSafely(slot);
            _items.Add(slot);

            // 新卡牌初始位置
            var topY = _items.Count > 1 ? _items[^2].Position.Y : 0;
            slot.Position = new Vector2(0, topY + ItemStep);
        }

        var tween = CreateTween();
        tween.TweenProperty(slot, "scale", Vector2.One, 0.8f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out)
            .Set("backStrength", 8f);

        if (isInstant)
        {
            tween.Finished += async () =>
            {
                await Cmd.Wait(0.2f);
                var outTween = CreateTween().TweenProperty(slot, "scale", Vector2.Zero, 0.4f)
                    .SetTrans(Tween.TransitionType.Back)
                    .SetEase(Tween.EaseType.In);
                outTween.Set("backStrength", 5f);
                outTween.Finished += async () => { await HandleCardRemoval(cardModel); };
            };
        }

        if (!isInstant && !UpdateItems())
            AnimateToFinalLayout();
    }

    private void OnCardRemoved(CardModel cardModel)
    {
        var removedItem = _items.Find(item => item.Model == cardModel);
        if (removedItem == null) return;

        var tween = CreateTween();
        tween.TweenProperty(removedItem, "scale", Vector2.Zero, 0.2f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.In)
            .Set("backStrength", 5f);
        tween.Finished += () =>
        {
            if (removedItem.Model is IPerformanceCard performanceCard)
            {
                performanceCard.Handle = null;
            }

            _items.Remove(removedItem);
            removedItem.QueueFreeSafely();

            if (!UpdateItems())
                AnimateToFinalLayout();
        };
    }

    // 需要重新计算布局时返回true
    private bool UpdateItems()
    {
        var needRemove = _items.Count - Capacity;

        switch (needRemove)
        {
            case 0:
                return false;
            case < 0:
                AddEmptyNode(-needRemove);
                return false;
            default:
                RemoveOverflowNode(needRemove);
                return true;
        }
    }

    private void AddEmptyNode(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var item = NPerformanceItem.Create(LocalContext.IsMe(Player));
            item.Scale = Vector2.Zero;
            _ItemContainer.AddChildSafely(item);
            _items.Add(item);

            var tween = CreateTween();
            tween.TweenProperty(item, "scale", Vector2.One, 0.8f)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out)
                .Set("backStrength", 8f);
        }
    }

    private void RemoveOverflowNode(int count)
    {
        var itemsToRemove = _items.Take(count).ToList();
        var pending = itemsToRemove.Count;

        for (var i = 0; i < count; i++)
        {
            var item = _items[i];
            var tween = CreateTween();
            tween.TweenProperty(item, "scale", Vector2.Zero, 0.2f)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.In)
                .Set("backStrength", 8f);
            tween.Finished += async () =>
            {
                _items.Remove(item);
                item.QueueFreeSafely();
                await HandleCardRemoval(item.Model);
                pending--;
                if (pending == 0)
                    AnimateToFinalLayout();
            };
        }
    }

    private async Task HandleCardRemoval(CardModel? card)
    {
        if (card == null || !PerformancePile.Cards.Contains(card)) return;
        var isDupe = card.IsDupe;
        var nextPile = PileType.Discard;
        if (card is IPerformanceCard performanceCard)
        {
            nextPile = performanceCard.WhenStopMoveToPile;

            await performanceCard.OnStopPerformance(new HookPlayerChoiceContext(card, card.Owner.NetId,
                card.CombatState!, GameActionType.Combat));
        }

        await BangDreamHook.OnCardLeavePerformanceArea(card.CombatState!, card);

        if (!isDupe)
        {
            if (nextPile == PileType.Discard)
            {
                await CardCmd.Discard(new ThrowingPlayerChoiceContext(), card);
            }
            else
            {
                await CardPileCmd.Add(card, nextPile.GetPile(card.Owner));
            }

            return;
        }

        await CardPileCmd.RemoveFromCombat(card);
    }

    private void AnimateToFinalLayout()
    {
        if (_items.Count == 0) return;

        var oldPositions = new Dictionary<NPerformanceItem, Vector2>();
        foreach (var item in _items)
            oldPositions[item] = item.Position;

        var targetPositions = new Dictionary<NPerformanceItem, Vector2>();
        for (var i = 0; i < _items.Count; i++)
        {
            var y = i * ItemStep;
            targetPositions[_items[i]] = new Vector2(0, y);
        }

        foreach (var item in _items)
            item.Position = oldPositions[item];

        var tween = CreateTween();
        tween.SetParallel();
        foreach (var item in _items)
        {
            if (targetPositions.TryGetValue(item, out var target))
            {
                tween.TweenProperty(item, "position", target, 0.25f)
                    .SetTrans(Tween.TransitionType.Quad)
                    .SetEase(Tween.EaseType.InOut);
            }
        }
    }

    private Vector2 GetSlotPos(int slotIndex)
    {
        var localPos = new Vector2(0, slotIndex * ItemStep);
        return _ItemContainer.GlobalPosition + localPos;
    }
}