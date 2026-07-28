using BangDreamLib.Scripts.Enums;
using BangDreamLib.Scripts.Interfaces;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Nodes;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;
using STS2RitsuLib.Utils;

namespace BangDreamLib.Scripts.Features;

public class PerformManager : SingletonModel, IInCombatManager, ISecondaryResourceHookListener
{
    private const string LocTable = "combat_messages";
    private const string MessagePrefix = "BANG_DREAM_LIB_PERFORM_MANAGER";
    private const string ZeroCapacityPostfix = ".zreo_capacity";
    private const string FullCapacityPostfix = ".full_capacity";

    private static readonly LocString EmptyThink = new(LocTable, MessagePrefix + ZeroCapacityPostfix);
    private static readonly LocString MaxSizeThink = new(LocTable, MessagePrefix + FullCapacityPostfix);

    private const int MaxCapacity = 7;

    public override bool ShouldReceiveCombatHooks => true;

    private Player? _player;
    private CardPile? _pile;

    private readonly Lock _performAreaChangeQueueLock = new();

    private readonly Queue<CardModel> _cardsAwaitingArrival = [];
    private readonly HashSet<CardModel> _cardsWithArrivalVisual = [];
    private readonly Queue<PerformAreaChange> _performAreaChanges = [];
    private readonly Dictionary<CardModel, int> _pendingPerformAreaAdditions = [];

    private bool _isProcessingPerformAreaChanges;

    public Player Player
    {
        get => _player ?? throw new InvalidOperationException("Owner is not Initialized.");
        set
        {
            AssertMutable();
            BangDreamTools.Init(ref _player, value, nameof(Player));
        }
    }


    public CardPile PerformPile
    {
        get => _pile ?? throw new InvalidOperationException("PerformPile is not Initialized.");
        set
        {
            AssertMutable();
            _pile = value;
        }
    }

    public int Capacity { get; private set; }

    public NPerformArea PerformArea { get; private set; } = null!;

    public readonly AttachedState<CardModel, PerformContext> CardContexts = new(cardModel =>
    {
        if (cardModel is IPerformCard performCard)
        {
            return new PerformContext(
                null,
                null,
                -1,
                performCard.Strategy,
                performCard.AspirationSlot
            );
        }

        return new PerformContext(null, null);
    });

    public void AddCapacity(int amount)
    {
        if (amount <= 0) return;
        if (Capacity == MaxCapacity)
        {
            ThinkCmd.Play(MaxSizeThink, Player.Creature, 1.5d);
            return;
        }

        Capacity = Math.Min(MaxCapacity, Capacity + amount);
        PerformArea.SetCapacity(Capacity);
    }

    public void ReduceCapacity(int amount)
    {
        if (amount <= 0) return;
        Capacity = Math.Max(0, Capacity - amount);
        PerformArea.SetCapacity(Capacity);
        TaskHelper.RunSafely(RemoveOverflowItems());
    }

    public async Task Clean()
    {
        foreach (var cardModel in PerformPile.Cards.ToList())
        {
            if (cardModel.Pile != PerformPile) continue;

            await Cmd.CustomScaledWait(0.15f, 0.3f);
            await MoveCardInternal(cardModel);
        }
    }

    private void OnCardAdded(CardModel cardModel)
    {
        if (NCard.FindOnTable(cardModel) != null)
        {
            _cardsAwaitingArrival.Enqueue(cardModel);
            _cardsWithArrivalVisual.Add(cardModel);
        }

        QueuePerformAreaChange(cardModel, PerformAreaChangeType.Added);
    }

    private void OnCardAddFinished()
    {
        if (!_cardsAwaitingArrival.TryDequeue(out var cardModel)) return;
        _cardsWithArrivalVisual.Remove(cardModel);
        if (cardModel.Pile == PerformPile)
        {
            PerformArea.PlayCardArrivalBounce(cardModel);
        }
    }

    private void OnCardRemoved(CardModel cardModel)
    {
        PerformArea.RemoveItem(cardModel);

        if (cardModel.CombatState == null && Player.Creature.CombatState == null)
        {
            CardContexts.Remove(cardModel);
            return;
        }

        QueuePerformAreaChange(cardModel, PerformAreaChangeType.Removed);
    }

    private async Task RemoveOverflowItems()
    {
        if (Capacity <= 0)
        {
            await Clean();
            return;
        }

        HashSet<CardModel> pendingAdditions;
        lock (_performAreaChangeQueueLock)
        {
            pendingAdditions = _pendingPerformAreaAdditions.Keys.ToHashSet();
        }

        var overflowItems = from cardModel in PerformPile.Cards
            let performContext = CardContexts.GetOrCreate(cardModel)
            where !pendingAdditions.Contains(cardModel) &&
                  (performContext.SlotIndex < 1 || performContext.SlotIndex > Capacity)
            select cardModel;

        foreach (var overflowItem in overflowItems.ToList())
        {
            await Cmd.CustomScaledWait(0.15f, 0.3f);
            await MoveCardInternal(overflowItem);
        }
    }

    private async Task HandleCardAddedInternal(PlayerChoiceContext choiceContext, CardModel cardModel)
    {
        ArgumentNullException.ThrowIfNull(cardModel.CombatState);

        if (!PerformPile.Cards.Contains(cardModel)) return;

        if (Capacity == 0)
        {
            ThinkCmd.Play(EmptyThink, Player.Creature, 1.5f);
            await MoveCardInternal(cardModel);
            return;
        }

        var performContext = CardContexts.GetOrCreate(cardModel);
        performContext.Manager = this;
        var enqueuePlan = CreateEnqueuePlan(cardModel);
        if (enqueuePlan == null)
        {
            ThinkCmd.Play(MaxSizeThink, Player.Creature, 1.5f);
            await MoveCardInternal(cardModel);
            return;
        }

        await ApplyEnqueuePlan(enqueuePlan, performContext);

        PerformArea.AddItem(cardModel, performContext, _cardsWithArrivalVisual.Contains(cardModel));

        await TryInstantInternal(cardModel);

        await BangDreamHook.OnCardEnterPerformArea(choiceContext, cardModel.CombatState, cardModel);

        await RemoveOverflowItems();
    }

    private async Task HandleCardRemovedInternal(PlayerChoiceContext choiceContext, CardModel cardModel)
    {
        var combatState = cardModel.CombatState ?? Player.Creature.CombatState;
        if (combatState == null)
        {
            CardContexts.Remove(cardModel);
            return;
        }

        if (PerformPile.Cards.Contains(cardModel))
        {
            await MoveCardInternal(cardModel);
        }

        await BangDreamHook.OnCardLeavePerformArea(choiceContext, combatState, cardModel);

        CardContexts.Remove(cardModel);

        await RemoveOverflowItems();
    }

    private async Task TryPerformInternal(int slotIndex, bool isSubsideTriggered = false)
    {
        var lingeredHitCard = (from pileCard in PerformPile.Cards
            let performContext = CardContexts.GetOrCreate(pileCard)
            where performContext.SlotIndex == slotIndex
            select pileCard).FirstOrDefault();

        if (lingeredHitCard is IPerformCard { IsInstant: false } performCard)
        {
            var performContext = CardContexts.GetOrCreate(lingeredHitCard);
            var previousSubsideState = performContext.IsSubsideTriggered;
            performContext.IsSubsideTriggered = isSubsideTriggered;
            try
            {
                await PerformCard(lingeredHitCard, performCard);
            }
            finally
            {
                performContext.IsSubsideTriggered = previousSubsideState;
            }

            BangDreamLibCore.Logger.Info(
                $"Player {Player.Character} ({Player.NetId}) Perform : {lingeredHitCard.Title}");
        }
    }

    private async Task TryInstantInternal(CardModel cardModel)
    {
        ArgumentNullException.ThrowIfNull(cardModel.CombatState);

        if (cardModel is IPerformCard { IsInstant: true } performCard)
        {
            await PerformCard(cardModel, performCard);

            BangDreamLibCore.Logger.Info(
                $"Player {Player.Character} ({Player.NetId}) Instant Perform : {cardModel.Title}");
        }
    }

    public async Task PerformCard(CardModel cardModel)
    {
        if (cardModel is IPerformCard performCard)
        {
            await PerformCard(cardModel, performCard);
        }
    }

    private async Task PerformCard(CardModel cardModel, IPerformCard performCard)
    {
        ArgumentNullException.ThrowIfNull(cardModel.CombatState);

        await BangDreamHook.RunPerformHookAction(cardModel.CombatState, cardModel, performCard.OnPerform);

        await BangDreamHook.OnCardPerform(cardModel.CombatState, CardContexts.GetOrCreate(cardModel), cardModel);
    }

    /// <summary>
    /// 将当前管理的歌单卡牌移动到指定槽位。若目标槽位已被占用，则交换两张卡牌的位置。
    /// 此操作不会触发卡牌进入或离开歌单的 Hook。
    /// </summary>
    /// <returns>卡牌和目标槽位均有效时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public bool TryMoveCardToSlot(CardModel cardModel, int targetSlotIndex)
    {
        ArgumentNullException.ThrowIfNull(cardModel);

        if (cardModel.Pile != PerformPile || !IsValidSlot(targetSlotIndex))
        {
            return false;
        }

        var cardContext = CardContexts.GetOrCreate(cardModel);
        if (cardContext.Manager != this || !IsValidSlot(cardContext.SlotIndex))
        {
            return false;
        }

        var sourceSlotIndex = cardContext.SlotIndex;
        if (sourceSlotIndex == targetSlotIndex)
        {
            return true;
        }

        var cardInTargetSlot = PerformPile.Cards.FirstOrDefault(card =>
            card != cardModel && CardContexts.GetOrCreate(card).SlotIndex == targetSlotIndex);

        cardContext.SlotIndex = targetSlotIndex;
        if (cardInTargetSlot != null)
        {
            CardContexts.GetOrCreate(cardInTargetSlot).SlotIndex = sourceSlotIndex;
        }

        PerformArea.RefreshItemLayout();
        return true;
    }

    /// <summary>
    /// 按指定顺序重排歌单，并让每张牌重新触发进入歌单时的效果。
    /// </summary>
    public async Task ReorderAndReenter(
        PlayerChoiceContext choiceContext,
        IReadOnlyList<CardModel> orderedCards)
    {
        if (orderedCards.Count != PerformPile.Cards.Count ||
            !orderedCards.ToHashSet().SetEquals(PerformPile.Cards))
        {
            throw new ArgumentException("Cards must contain every card currently in the perform pile.",
                nameof(orderedCards));
        }

        for (var index = 0; index < orderedCards.Count; index++)
        {
            var context = CardContexts.GetOrCreate(orderedCards[index]);
            context.Manager = this;
            context.SlotIndex = index + 1;
        }

        PerformArea.RefreshItemLayout();

        foreach (var card in orderedCards)
        {
            if (card.Pile != PerformPile || card.CombatState == null) continue;

            await TryInstantInternal(card);
            await BangDreamHook.OnCardEnterPerformArea(choiceContext, card.CombatState, card);
        }
    }

    public int GetExpectedSlotIndex(CardModel cardModel)
    {
        return CreateEnqueuePlan(cardModel)?.SlotIndex ?? -1;
    }

    public CardModel? GetCardDisplacedBy(CardModel incomingCard)
    {
        return CreateEnqueuePlan(incomingCard)?.DisplacedCard;
    }

    private async Task ApplyEnqueuePlan(EnqueuePlan plan, PerformContext incomingContext)
    {
        if (plan.DisplacedCard != null)
        {
            await MoveCardInternal(plan.DisplacedCard);
        }

        foreach (var change in plan.SlotChanges)
        {
            change.Context.SlotIndex = change.SlotIndex;
        }

        incomingContext.SlotIndex = plan.SlotIndex;
        if (plan.SlotChanges.Count > 0)
        {
            PerformArea.RefreshItemLayout();
            foreach (var change in plan.SlotChanges)
            {
                change.Context.Slot?.PlayPortraitReveal();
            }
        }
    }

    private EnqueuePlan? CreateEnqueuePlan(CardModel incomingCard)
    {
        if (Capacity <= 0) return null;

        var incomingContext = CardContexts.GetOrCreate(incomingCard);
        if (IsValidSlot(incomingContext.SlotIndex))
        {
            return new EnqueuePlan(incomingContext.SlotIndex, null, []);
        }

        var occupiedSlots = PerformPile.Cards
            .Where(card => card != incomingCard)
            .Select(card => new OccupiedSlot(card, CardContexts.GetOrCreate(card)))
            .Where(slot => IsValidSlot(slot.Context.SlotIndex))
            .ToDictionary(slot => slot.Context.SlotIndex);

        if (incomingContext.Strategy == PerformEnqueueStrategy.Fixed &&
            IsValidSlot(incomingContext.AspirationSlot))
        {
            occupiedSlots.TryGetValue(incomingContext.AspirationSlot, out var displacedSlot);
            return new EnqueuePlan(incomingContext.AspirationSlot, displacedSlot?.Card, []);
        }

        if (incomingContext.Strategy is not (PerformEnqueueStrategy.Default or PerformEnqueueStrategy.Fixed))
        {
            var aspirationSlot = Math.Clamp(incomingContext.AspirationSlot, 1, Capacity);
            var availableSlot = EnumerateCandidateSlots(incomingContext.Strategy, aspirationSlot)
                .FirstOrDefault(slotIndex => !occupiedSlots.ContainsKey(slotIndex));
            if (availableSlot > 0)
            {
                return new EnqueuePlan(availableSlot, null, []);
            }
        }

        var firstAvailableSlot = Enumerable.Range(1, Capacity)
            .FirstOrDefault(slotIndex => !occupiedSlots.ContainsKey(slotIndex));
        if (firstAvailableSlot > 0)
        {
            var slotChanges = occupiedSlots.Values
                .Where(slot => slot.Context.SlotIndex < firstAvailableSlot)
                .Select(slot => new SlotChange(slot.Context, slot.Context.SlotIndex + 1))
                .ToList();
            return new EnqueuePlan(1, null, slotChanges);
        }

        occupiedSlots.TryGetValue(Capacity, out var overflowSlot);
        var overflowSlotChanges = occupiedSlots.Values
            .Where(slot => slot.Context.SlotIndex < Capacity)
            .Select(slot => new SlotChange(slot.Context, slot.Context.SlotIndex + 1))
            .ToList();
        return new EnqueuePlan(1, overflowSlot?.Card, overflowSlotChanges);
    }

    private bool IsValidSlot(int slotIndex)
    {
        return slotIndex >= 1 && slotIndex <= Capacity;
    }

    private IEnumerable<int> EnumerateCandidateSlots(PerformEnqueueStrategy strategy, int aspirationSlot)
    {
        return strategy switch
        {
            PerformEnqueueStrategy.Bottom => EnumerateBottomFirst(aspirationSlot),
            PerformEnqueueStrategy.Top => EnumerateTopFirst(aspirationSlot),
            _ => EnumerateNearbyFirst(aspirationSlot)
        };
    }

    private IEnumerable<int> EnumerateNearbyFirst(int aspirationSlot)
    {
        yield return aspirationSlot;

        for (var offset = 1; offset < Capacity; offset++)
        {
            var lower = aspirationSlot - offset;
            if (lower >= 1)
            {
                yield return lower;
            }

            var upper = aspirationSlot + offset;
            if (upper <= Capacity)
            {
                yield return upper;
            }
        }
    }

    private IEnumerable<int> EnumerateTopFirst(int aspirationSlot)
    {
        for (var slotIndex = aspirationSlot; slotIndex <= Capacity; slotIndex++)
        {
            yield return slotIndex;
        }

        for (var slotIndex = aspirationSlot - 1; slotIndex >= 1; slotIndex--)
        {
            yield return slotIndex;
        }
    }

    private IEnumerable<int> EnumerateBottomFirst(int aspirationSlot)
    {
        for (var slotIndex = aspirationSlot; slotIndex >= 1; slotIndex--)
        {
            yield return slotIndex;
        }

        for (var slotIndex = aspirationSlot + 1; slotIndex <= Capacity; slotIndex++)
        {
            yield return slotIndex;
        }
    }

    public async Task AfterSecondaryResourceChanged(SecondaryResourceChangeContext ctx)
    {
        if (ctx.Player == _player && ctx.Definition.Id.Equals(BangDreamConst.LingeredResource))
        {
            if (ctx.NewAmount > 0 && ctx.NewAmount <= Capacity)
            {
                await TryPerformInternal(ctx.NewAmount, ctx.Reason == SecondaryResourceChangeReason.Spend);
            }
        }
    }

    public override CardLocation ModifyCardPlayResultLocation(CardModel card, bool isAutoPlay, ResourceInfo resources,
        CardLocation cardLocation)
    {
        if (cardLocation.player == Player && card is IPerformCard performanceCard &&
            cardLocation.pileType == PileType.Discard)
        {
            return performanceCard.StopPerformanceNextPile();
        }

        return cardLocation;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card is IPerformCard)
        {
            await Cmd.CustomScaledWait(0.35f, 0.5f);
        }
    }

    public void SubmitCombatState()
    {
        PerformPile = BangDreamConst.PerformPile.GetPile(Player);

        if (ModNodeAttachmentRegistry.For(BangDreamConst.ModId).TryGetAttached<NCreature, NPerformArea>(
                Player.Creature.GetCreatureNode()!, "perform_area", out var areaNode))
        {
            PerformArea = areaNode;
        }

        if (Player.Character is IPerformableCharacter character)
            Capacity = character.GetDefaultCapacity;
        else
            Capacity = 0;

        PerformArea.SetCapacity(Capacity);
        PerformArea.SubmitChanged();

        PerformPile.CardAdded += OnCardAdded;
        PerformPile.CardAddFinished += OnCardAddFinished;
        PerformPile.CardRemoved += OnCardRemoved;
        _cardsAwaitingArrival.Clear();
        _cardsWithArrivalVisual.Clear();
        ClearPerformAreaChanges();
        CardContexts.Clear();
    }

    public void UnsubscribeCombatState()
    {
        PerformPile.CardAdded -= OnCardAdded;
        PerformPile.CardAddFinished -= OnCardAddFinished;
        PerformPile.CardRemoved -= OnCardRemoved;
        _cardsAwaitingArrival.Clear();
        _cardsWithArrivalVisual.Clear();
        ClearPerformAreaChanges();
        CardContexts.Clear();
    }

    private static async Task MoveCardInternal(CardModel cardModel)
    {
        if (cardModel.IsDupe)
        {
            await CardPileCmd.RemoveFromCombat(cardModel);
            return;
        }

        CardLocation location;
        if (cardModel is IPerformCard performanceCard)
        {
            location = performanceCard.StopPerformanceNextPile();
        }
        else
        {
            location = new CardLocation(cardModel.Owner, PileType.Discard, CardPilePosition.Bottom);
        }

        if (location.pileType == PileType.None)
        {
            await CardPileCmd.RemoveFromCombat(cardModel);
        }
        else
        {
            await CardPileCmd.Add(cardModel, location.pileType, location.position);
        }
    }

    private void QueuePerformAreaChange(CardModel cardModel, PerformAreaChangeType type)
    {
        var combatState = cardModel.CombatState ?? Player.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        var shouldStartProcessing = false;
        lock (_performAreaChangeQueueLock)
        {
            _performAreaChanges.Enqueue(new PerformAreaChange(combatState, cardModel, type));
            if (type == PerformAreaChangeType.Added)
            {
                _pendingPerformAreaAdditions.TryGetValue(cardModel, out var pendingCount);
                _pendingPerformAreaAdditions[cardModel] = pendingCount + 1;
            }

            if (!_isProcessingPerformAreaChanges)
            {
                _isProcessingPerformAreaChanges = true;
                shouldStartProcessing = true;
            }
        }

        if (shouldStartProcessing)
        {
            TaskHelper.RunSafely(ProcessPerformAreaChanges());
        }
    }

    private async Task ProcessPerformAreaChanges()
    {
        while (TryDequeuePerformAreaChange(out var change))
        {
            try
            {
                await BangDreamHook.RunPerformHookAction(
                    change.CombatState,
                    change.CardModel,
                    choiceContext => HandlePerformAreaChange(choiceContext, change));
            }
            catch (Exception exception)
            {
                BangDreamLibCore.Logger.Error(
                    $"Failed to process perform area {change.Type} hook for {change.CardModel.Title}: {exception}");
            }
            finally
            {
                if (change.Type == PerformAreaChangeType.Added)
                {
                    CompletePendingPerformAreaAddition(change.CardModel);
                }
            }
        }
    }

    private bool TryDequeuePerformAreaChange(out PerformAreaChange change)
    {
        lock (_performAreaChangeQueueLock)
        {
            if (_performAreaChanges.Count > 0)
            {
                change = _performAreaChanges.Dequeue();
                return true;
            }

            _isProcessingPerformAreaChanges = false;
            change = default;
            return false;
        }
    }

    private Task HandlePerformAreaChange(PlayerChoiceContext choiceContext, PerformAreaChange change)
    {
        return change.Type switch
        {
            PerformAreaChangeType.Added => HandleCardAddedInternal(choiceContext, change.CardModel),
            PerformAreaChangeType.Removed => HandleCardRemovedInternal(choiceContext, change.CardModel),
            _ => throw new ArgumentOutOfRangeException(nameof(change), $"Unknown change type: {change.Type}")
        };
    }

    private void CompletePendingPerformAreaAddition(CardModel cardModel)
    {
        lock (_performAreaChangeQueueLock)
        {
            if (!_pendingPerformAreaAdditions.TryGetValue(cardModel, out var pendingCount)) return;

            if (pendingCount <= 1)
            {
                _pendingPerformAreaAdditions.Remove(cardModel);
            }
            else
            {
                _pendingPerformAreaAdditions[cardModel] = pendingCount - 1;
            }
        }
    }

    private void ClearPerformAreaChanges()
    {
        lock (_performAreaChangeQueueLock)
        {
            _performAreaChanges.Clear();
            _pendingPerformAreaAdditions.Clear();
        }
    }

    private enum PerformAreaChangeType
    {
        Added,
        Removed
    }

    private sealed record OccupiedSlot(CardModel Card, PerformContext Context);

    private sealed record SlotChange(PerformContext Context, int SlotIndex);

    private sealed record EnqueuePlan(int SlotIndex, CardModel? DisplacedCard, IReadOnlyList<SlotChange> SlotChanges);

    private readonly record struct PerformAreaChange(
        ICombatState CombatState,
        CardModel CardModel,
        PerformAreaChangeType Type);
}