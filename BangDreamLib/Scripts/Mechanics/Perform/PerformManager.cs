using System.Text.Json;
using BangDreamLib.Scripts.Enums;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Nodes;
using BangDreamLib.Scripts.Nodes.SubNode;
using BangDreamLib.Scripts.Nodes.VFX;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
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
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Networking.ManagedActions;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;
using STS2RitsuLib.Utils;

namespace BangDreamLib.Scripts.Mechanics.Perform;

public class PerformManager : SingletonModel, IInCombatManager, ISecondaryResourceHookListener
{
    private enum PerformNetworkActionKind
    {
        Enter,
        Leave,
        Trigger
    }

    private sealed record PerformNetworkActionPayload(
        PerformNetworkActionKind Kind,
        uint? CardIndex,
        int SlotIndex,
        bool IsSubsideTriggered);

    private static readonly RitsuLibManagedNetActionDescriptor<PerformNetworkActionPayload> PerformNetworkAction = new(
        "BangDreamLib", "perform_lifecycle",
        static payload => JsonSerializer.SerializeToUtf8Bytes(payload),
        static bytes => JsonSerializer.Deserialize<PerformNetworkActionPayload>(bytes) ??
                        throw new InvalidOperationException("Invalid perform lifecycle payload."),
        static context => ExecuteNetworkPerformAction(context), GameActionType.Combat);

    internal static void InitializeNetwork()
    {
        RitsuLibManagedNetActions.Register(PerformNetworkAction);
    }

    /// <summary>
    /// 在原版同步战斗开始 Hook 内将牌加入歌单并完成进入结算，不跨初始化边界另排网络 Action。
    /// </summary>
    public async Task AddInitialCard(CardModel cardModel)
    {
        ArgumentNullException.ThrowIfNull(cardModel);
        var combatState = Player.Creature.CombatState;
        if (combatState == null) return;

        _initialPerformAreaAdditions.Add(cardModel);
        try
        {
            await CardPileCmd.Add(cardModel, BangDreamConst.PerformPile);
            if (cardModel.Pile != PerformPile) return;

            var context = CardContexts.GetOrCreate(cardModel);
            context.Manager = this;
            _pendingPerformAreaAdditions.Add(cardModel);
            await BangDreamHook.RunPerformHookAction(
                combatState,
                cardModel,
                choiceContext => HandleCardAddedInternal(choiceContext, cardModel));
        }
        finally
        {
            _initialPerformAreaAdditions.Remove(cardModel);
        }
    }

    private static Task ExecuteNetworkPerformAction(
        RitsuLibManagedNetActionContext<PerformNetworkActionPayload> context)
    {
        var manager = context.Player.AttachedData().PerformManager;
        return manager.ExecuteNetworkPerformAction(context.Message, context.PlayerChoiceContext);
    }

    private async Task ExecuteNetworkPerformAction(
        PerformNetworkActionPayload payload,
        PlayerChoiceContext choiceContext)
    {
        var combatState = Player.Creature.CombatState;
        if (combatState == null) return;
        if (payload.Kind == PerformNetworkActionKind.Trigger)
        {
            await TryPerformInternal(payload.SlotIndex, payload.IsSubsideTriggered, choiceContext);
            return;
        }

        if (!payload.CardIndex.HasValue) return;
        var card = NetCombatCard.ForTesting(payload.CardIndex.Value).ToCardModelOrNull();
        if (card == null) return;
        var change = new PerformAreaChange(card,
            payload.Kind == PerformNetworkActionKind.Enter
                ? PerformAreaChangeType.Added
                : PerformAreaChangeType.Removed);
        await HandlePerformAreaChange(choiceContext, change);
    }

    private async Task ExecuteLocalPerformAction(PerformNetworkActionPayload payload)
    {
        var combatState = Player.Creature.CombatState;
        if (combatState == null) return;
        if (payload.Kind == PerformNetworkActionKind.Trigger)
        {
            await TryPerformInternal(payload.SlotIndex, payload.IsSubsideTriggered);
            return;
        }

        if (!payload.CardIndex.HasValue) return;
        var card = NetCombatCard.ForTesting(payload.CardIndex.Value).ToCardModelOrNull();
        if (card == null) return;
        var change = new PerformAreaChange(card,
            payload.Kind == PerformNetworkActionKind.Enter
                ? PerformAreaChangeType.Added
                : PerformAreaChangeType.Removed);
        await BangDreamHook.RunPerformHookAction(
            combatState, card, choiceContext => HandlePerformAreaChange(choiceContext, change));
    }

    private const string LocTable = "combat_messages";
    private const string MessagePrefix = "BANG_DREAM_LIB_PERFORM_MANAGER";
    private const string ZeroCapacityPostfix = ".zero_capacity";
    private const string FullCapacityPostfix = ".full_capacity";
    private const string PerformFlashVfxPath = "res://BangDreamLib/scenes/vfx/perform_flash_vfx.tscn";

    private static readonly LocString EmptyThink = new(LocTable, MessagePrefix + ZeroCapacityPostfix);
    private static readonly LocString MaxSizeThink = new(LocTable, MessagePrefix + FullCapacityPostfix);

    private const int MaxCapacity = 7;

    public override bool ShouldReceiveCombatHooks => true;

    private Player? _player;
    private CardPile? _pile;

    private readonly Queue<CardModel> _cardsAwaitingArrival = [];
    private readonly HashSet<CardModel> _cardsWithArrivalVisual = [];
    private readonly HashSet<CardModel> _cardsPendingArrival = [];
    private readonly HashSet<CardModel> _pendingPerformAreaAdditions = [];
    private readonly HashSet<CardModel> _initialPerformAreaAdditions = [];
    private readonly HashSet<CardModel> _instantPerformedCards = [];


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
        if (!CombatManager.Instance.IsInProgress)
            return;

        // 原版 NetCombatCardDb 仅在牌堆 ContentsChanged 时登记 ID，而 CardAdded 早于
        // ContentsChanged 触发；此处提前登记，保证 QueuePerformAreaChange 取索引时已有 ID。
        NetCombatCardDb.Instance.IdCardForTesting(cardModel);

        if (Capacity > 0)
        {
            var context = CardContexts.GetOrCreate(cardModel);
            context.Manager = this;
            _pendingPerformAreaAdditions.Add(cardModel);
        }

        if (HasCardArrivalVisual(cardModel))
        {
            _cardsAwaitingArrival.Enqueue(cardModel);
            _cardsWithArrivalVisual.Add(cardModel);
            _cardsPendingArrival.Add(cardModel);
        }

        if (_initialPerformAreaAdditions.Contains(cardModel)) return;
        QueuePerformAreaChange(cardModel, PerformAreaChangeType.Added);
    }

    private static bool HasCardArrivalVisual(CardModel cardModel)
    {
        return NCard.FindOnTable(cardModel, PileType.Play) != null ||
               NCard.FindOnTable(cardModel, PileType.Hand) != null;
    }

    private void OnCardAddFinished()
    {
        if (!_cardsAwaitingArrival.TryDequeue(out var cardModel)) return;
        _cardsWithArrivalVisual.Remove(cardModel);
        if (cardModel.Pile == PerformPile)
        {
            PerformArea.PlayCardArrivalBounce(cardModel);
        }

        QueuePerformAreaChange(cardModel, PerformAreaChangeType.Arrived);
    }

    private void OnCardRemoved(CardModel cardModel)
    {
        if (!CombatManager.Instance.IsInProgress)
            return;

        PerformArea.RemoveItem(cardModel);
        RemoveAwaitingArrival(cardModel);
        _cardsWithArrivalVisual.Remove(cardModel);
        if (_cardsPendingArrival.Remove(cardModel))
        {
            CompletePendingPerformAreaAddition(cardModel);
        }

        if (cardModel.CombatState == null && Player.Creature.CombatState == null)
        {
            CardContexts.Remove(cardModel);
            return;
        }

        QueuePerformAreaChange(cardModel, PerformAreaChangeType.Removed);
    }

    private void RemoveAwaitingArrival(CardModel cardModel)
    {
        if (!_cardsAwaitingArrival.Contains(cardModel)) return;

        var remainingCards = _cardsAwaitingArrival.Where(card => card != cardModel).ToList();
        _cardsAwaitingArrival.Clear();
        foreach (var remainingCard in remainingCards)
        {
            _cardsAwaitingArrival.Enqueue(remainingCard);
        }
    }

    private async Task RemoveOverflowItems()
    {
        if (Capacity <= 0)
        {
            await Clean();
            return;
        }

        HashSet<CardModel> pendingAdditions = [.. _pendingPerformAreaAdditions];

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

        if (Capacity == 0)
        {
            ThinkCmd.Play(EmptyThink, Player.Creature, 1.5f);
            if (PerformPile.Cards.Contains(cardModel)) await MoveCardInternal(cardModel);
            return;
        }

        var performContext = CardContexts.GetOrCreate(cardModel);
        performContext.Manager = this;
        var enqueuePlan = CreateEnqueuePlan(cardModel);
        if (enqueuePlan == null)
        {
            ThinkCmd.Play(MaxSizeThink, Player.Creature, 1.5f);
            if (PerformPile.Cards.Contains(cardModel)) await MoveCardInternal(cardModel);
            return;
        }

        try
        {
            await ApplyEnqueuePlan(enqueuePlan, performContext);

            if (!_cardsPendingArrival.Contains(cardModel) && PerformPile.Cards.Contains(cardModel))
                PerformArea.AddItem(cardModel, performContext, _cardsWithArrivalVisual.Contains(cardModel));

            await TryInstantInternal(cardModel, choiceContext);

            await BangDreamHook.OnCardEnterPerformArea(choiceContext, cardModel.CombatState, cardModel);

            await RemoveOverflowItems();
        }
        finally
        {
            CompletePendingPerformAreaAddition(cardModel);
        }
    }

    private Task HandleCardArrivedInternal(CardModel cardModel)
    {
        _cardsPendingArrival.Remove(cardModel);

        if (!PerformPile.Cards.Contains(cardModel) || cardModel.CombatState == null) return Task.CompletedTask;

        var performContext = CardContexts.GetOrCreate(cardModel);
        if (performContext.Manager != this || !IsValidSlot(performContext.SlotIndex)) return Task.CompletedTask;

        PerformArea.AddItem(cardModel, performContext, waitForCardArrival: false);
        return Task.CompletedTask;
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

    private async Task TryPerformInternal(
        int slotIndex,
        bool isSubsideTriggered = false,
        PlayerChoiceContext? choiceContext = null)
    {
        var slotCard = (from pileCard in PerformPile.Cards
            let performContext = CardContexts.GetOrCreate(pileCard)
            where performContext.SlotIndex == slotIndex
            select pileCard).FirstOrDefault();

        if (slotCard is IPerformCard { IsInstant: false } performCard)
        {
            var performContext = CardContexts.GetOrCreate(slotCard);
            var previousSubsideState = performContext.IsSubsideTriggered;
            performContext.IsSubsideTriggered = isSubsideTriggered;
            try
            {
                await PerformCard(slotCard, performCard, true, choiceContext);
            }
            finally
            {
                performContext.IsSubsideTriggered = previousSubsideState;
            }

            BangDreamLibCore.Logger.Info(
                $"Player {Player.Character} ({Player.NetId}) Perform : {slotCard.Title}");
        }
    }

    private async Task TryInstantInternal(CardModel cardModel, PlayerChoiceContext? choiceContext = null)
    {
        ArgumentNullException.ThrowIfNull(cardModel.CombatState);

        if (cardModel is IPerformCard { IsInstant: true } performCard)
        {
            if (!_instantPerformedCards.Add(cardModel))
            {
#if DEBUG
                BangDreamLibCore.Logger.Info(
                    $"Ignored duplicate instant perform: player={Player.NetId} card={cardModel.Id} instance={cardModel.GetHashCode()}");
#endif
                return;
            }

            await PerformCard(cardModel, performCard, true, choiceContext);

            BangDreamLibCore.Logger.Info(
                $"Player {Player.Character} ({Player.NetId}) Instant Perform : {cardModel.Title}");
        }
    }

    public async Task PerformCard(CardModel cardModel, bool isAutoPerform = false)
    {
        if (cardModel is IPerformCard performCard)
        {
            await PerformCard(cardModel, performCard, isAutoPerform);
        }
    }

    private async Task PerformCard(
        CardModel cardModel,
        IPerformCard performCard,
        bool isAutoPerform,
        PlayerChoiceContext? choiceContext = null)
    {
        ArgumentNullException.ThrowIfNull(cardModel.CombatState);

        PlayPerformFlashVfx(cardModel, performCard);

        var performContext = CardContexts.GetOrCreate(cardModel);
        var perform = new CardPerform
        {
            Card = cardModel,
            Player = Player,
            SlotIndex = performContext.SlotIndex,
            IsAutoPerform = isAutoPerform,
            IsInstant = performCard.IsInstant,
            IsSubsideTriggered = performContext.IsSubsideTriggered
        };

        if (choiceContext == null)
        {
            await BangDreamHook.RunPerformHookAction(
                cardModel.CombatState, cardModel, context => performCard.OnPerform(context, perform));
            await BangDreamHook.OnCardPerform(cardModel.CombatState, perform);
        }
        else
        {
            await BangDreamHook.RunPerformHookAction(
                choiceContext, cardModel, context => performCard.OnPerform(context, perform));
            await BangDreamHook.OnCardPerform(choiceContext, cardModel.CombatState, perform);
        }
    }

    private void PlayPerformFlashVfx(CardModel cardModel, IPerformCard performCard)
    {
        if (!PerformArea.IsInsideTree()) return;
        if (!PerformArea.TryGetCardSlotCenter(cardModel, out var slotCenter)) return;

        var vfx = BangDreamPreloadManager.GetScene(PerformFlashVfxPath).Instantiate<PerformFlashVfx>();
        vfx.FlashColor = NPerformItem.GetSlotColor(performCard);
        vfx.Scale = Vector2.One * PerformArea.ItemScale;

        PerformArea.AddChildSafely(vfx);
        vfx.GlobalPosition = slotCenter;
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
        if (!CombatManager.Instance.IsInProgress)
            return;

        if (ctx.Player == _player && ctx.Definition.Id.Equals(BangDreamConst.LingeredResource))
        {
            if (ctx.NewAmount > 0 && ctx.NewAmount <= Capacity)
            {
                if (LocalContext.IsMe(Player))
                {
                    var payload = new PerformNetworkActionPayload(
                        PerformNetworkActionKind.Trigger, null, ctx.NewAmount,
                        ctx.Reason == SecondaryResourceChangeReason.Spend);
                    if (!RitsuLibManagedNetActions.Request(null, PerformNetworkAction, payload, Player.NetId))
                        await HandleRejectedNetworkAction(payload);
                }
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
        _cardsPendingArrival.Clear();
        _initialPerformAreaAdditions.Clear();
        ClearPerformAreaChanges();
        _instantPerformedCards.Clear();
        CardContexts.Clear();
    }

    public void UnsubscribeCombatState()
    {
        PerformPile.CardAdded -= OnCardAdded;
        PerformPile.CardAddFinished -= OnCardAddFinished;
        PerformPile.CardRemoved -= OnCardRemoved;
        _cardsAwaitingArrival.Clear();
        _cardsWithArrivalVisual.Clear();
        _cardsPendingArrival.Clear();
        _initialPerformAreaAdditions.Clear();
        ClearPerformAreaChanges();
        _instantPerformedCards.Clear();
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

        if (type == PerformAreaChangeType.Arrived)
        {
            TaskHelper.RunSafely(HandleCardArrivedInternal(cardModel));
            return;
        }

        if (!LocalContext.IsMe(Player)) return;
        var index = NetCombatCard.FromModel(cardModel).CombatCardIndex;
        var payload = new PerformNetworkActionPayload(
            type == PerformAreaChangeType.Added ? PerformNetworkActionKind.Enter : PerformNetworkActionKind.Leave,
            index,
            0,
            false);
        if (!RitsuLibManagedNetActions.Request(null, PerformNetworkAction, payload, Player.NetId))
            TaskHelper.RunSafely(HandleRejectedNetworkAction(payload));
    }

    private Task HandleRejectedNetworkAction(PerformNetworkActionPayload payload)
    {
        if (RunManager.Instance.NetService.Type == NetGameType.Singleplayer)
            return ExecuteLocalPerformAction(payload);

        BangDreamLibCore.Logger.Error(
            $"Perform action was rejected by the Sidecar action queue: player={Player.NetId} kind={payload.Kind}.");
        return Task.CompletedTask;
    }

    private Task HandlePerformAreaChange(PlayerChoiceContext choiceContext, PerformAreaChange change)
    {
        return change.Type switch
        {
            PerformAreaChangeType.Added => HandleCardAddedInternal(choiceContext, change.CardModel),
            PerformAreaChangeType.Removed => HandleCardRemovedInternal(choiceContext, change.CardModel),
            PerformAreaChangeType.Arrived => HandleCardArrivedInternal(change.CardModel),
            _ => throw new ArgumentOutOfRangeException(nameof(change), $"Unknown change type: {change.Type}")
        };
    }

    private void CompletePendingPerformAreaAddition(CardModel cardModel)
    {
        _pendingPerformAreaAdditions.Remove(cardModel);
    }

    private void ClearPerformAreaChanges()
    {
        _pendingPerformAreaAdditions.Clear();
    }

    private enum PerformAreaChangeType
    {
        Added,
        Removed,
        Arrived
    }

    private sealed record OccupiedSlot(CardModel Card, PerformContext Context);

    private sealed record SlotChange(PerformContext Context, int SlotIndex);

    private sealed record EnqueuePlan(int SlotIndex, CardModel? DisplacedCard, IReadOnlyList<SlotChange> SlotChanges);

    private readonly record struct PerformAreaChange(
        CardModel CardModel,
        PerformAreaChangeType Type);
}