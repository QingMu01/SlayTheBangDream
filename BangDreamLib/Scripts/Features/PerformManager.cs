using BangDreamLib.Scripts.Enums;
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
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;
using STS2RitsuLib.Utils;

namespace BangDreamLib.Scripts.Features;

public class PerformManager : SingletonModel, ISecondaryResourceHookListener
{
    private const string LocTable = "combat_messages";
    private const string MessagePrefix = "BANG_DREAM_LIB_PERFORM_MANAGER";
    private const string ZeroCapacityPostfix = ".zreo_capacity";
    private const string FullCapacityPostfix = ".full_capacity";

    private static readonly LocString EmptyThink = new(LocTable, MessagePrefix + ZeroCapacityPostfix);
    private static readonly LocString MaxSizeThink = new(LocTable, MessagePrefix + FullCapacityPostfix);

    private const int MaxCapacity = 7;

    public override bool ShouldReceiveCombatHooks => true;

    private bool _isSubscribed;

    private Player? _player;
    private CardPile? _pile;
    private readonly Queue<CardModel> _cardsAwaitingArrival = [];

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
            BangDreamTools.Init(ref _pile, value, nameof(PerformPile));
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

    private void OnCardAdded(CardModel cardModel)
    {
        _cardsAwaitingArrival.Enqueue(cardModel);
        RunPerformAreaChangedHook(cardModel, HandleCardAddedInternal);
    }

    private void OnCardAddFinished()
    {
        if (!_cardsAwaitingArrival.TryDequeue(out var cardModel)) return;
        if (cardModel.Pile == PerformPile)
        {
            PerformArea.PlayCardArrivalBounce(cardModel);
        }
    }

    private void OnCardRemoved(CardModel cardModel)
    {
        PerformArea.RemoveItem(cardModel);

        if (cardModel.CombatState == null)
        {
            CardContexts.Remove(cardModel);
            return;
        }

        RunPerformAreaChangedHook(cardModel, HandleCardRemovedInternal);
    }

    private async Task RemoveOverflowItems()
    {
        if (Capacity <= 0)
        {
            foreach (var cardModel in PerformPile.Cards.ToList())
            {
                await Cmd.CustomScaledWait(0.15f, 0.3f);
                await MoveCardInternal(cardModel);
            }

            return;
        }

        var overflowItems = from cardModel in PerformPile.Cards
            let performContext = CardContexts.GetOrCreate(cardModel)
            where performContext.SlotIndex < 1 || performContext.SlotIndex > Capacity
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
        if (!await TryAssignSlot(cardModel, performContext))
        {
            if (!await MakeRoomForNewCard(cardModel, performContext))
            {
                ThinkCmd.Play(MaxSizeThink, Player.Creature, 1.5f);
                await MoveCardInternal(cardModel);
                return;
            }
        }

        PerformArea.AddItem(cardModel, performContext);

        await TryInstantInternal(cardModel);

        await BangDreamHook.OnCardEnterPerformArea(choiceContext, cardModel.CombatState, cardModel);

        await RemoveOverflowItems();
    }

    private async Task HandleCardRemovedInternal(PlayerChoiceContext choiceContext, CardModel cardModel)
    {
        ArgumentNullException.ThrowIfNull(cardModel.CombatState);

        if (PerformPile.Cards.Contains(cardModel))
        {
            await MoveCardInternal(cardModel);
        }

        await BangDreamHook.OnCardLeavePerformArea(choiceContext, cardModel.CombatState, cardModel);

        CardContexts.Remove(cardModel);

        await RemoveOverflowItems();
    }

    private async Task TryPerformInternal(ICombatState combatState, int slotIndex)
    {
        var lingeredHitCard = (from pileCard in PerformPile.Cards
            let performContext = CardContexts.GetOrCreate(pileCard)
            where performContext.SlotIndex == slotIndex
            select pileCard).FirstOrDefault();

        if (lingeredHitCard is IPerformCard { IsInstant: false } performCard)
        {
            await BangDreamHook.RunPerformHookAction(combatState, lingeredHitCard,
                choiceContext => performCard.OnPerform(choiceContext));

            await BangDreamHook.OnCardPerform(combatState, CardContexts.GetOrCreate(lingeredHitCard),
                lingeredHitCard);

            BangDreamLibCore.Logger.Info(
                $"Player {Player.Character} ({Player.NetId}) Perform : {lingeredHitCard.Title}");
        }
    }

    private async Task TryInstantInternal(CardModel cardModel)
    {
        ArgumentNullException.ThrowIfNull(cardModel.CombatState);

        if (cardModel is IPerformCard { IsInstant: true } performCard)
        {
            await BangDreamHook.RunPerformHookAction(cardModel.CombatState, cardModel,
                choiceContext => performCard.OnPerform(choiceContext));

            await BangDreamHook.OnCardPerform(cardModel.CombatState, CardContexts.GetOrCreate(cardModel),
                cardModel);

            BangDreamLibCore.Logger.Info(
                $"Player {Player.Character} ({Player.NetId}) Instant Perform : {cardModel.Title}");
        }
    }

    public int GetExpectedSlotIndex(CardModel cardModel)
    {
        if (Capacity <= 0) return -1;

        var context = CardContexts.GetOrCreate(cardModel);
        if (context.SlotIndex >= 1 && context.SlotIndex <= Capacity)
        {
            return context.SlotIndex;
        }

        if (context.Strategy == PerformEnqueueStrategy.Fixed &&
            context.AspirationSlot >= 1 && context.AspirationSlot <= Capacity)
        {
            return context.AspirationSlot;
        }

        var availableSlot = FindAvailableSlot(cardModel, context);
        return availableSlot >= 1 && availableSlot <= Capacity ? availableSlot : 1;
    }

    private async Task<bool> TryAssignSlot(CardModel cardModel, PerformContext performContext)
    {
        if (Capacity <= 0)
        {
            return false;
        }

        if (performContext.Strategy == PerformEnqueueStrategy.Fixed)
        {
            if (performContext.AspirationSlot >= 1 && performContext.AspirationSlot <= Capacity)
            {
                var occupyingCard = FindCardInSlot(performContext.AspirationSlot, cardModel);
                if (occupyingCard != null)
                {
                    await MoveCardInternal(occupyingCard);
                }

                performContext.SlotIndex = performContext.AspirationSlot;
                return true;
            }
        }

        var slotIndex = FindAvailableSlot(cardModel, performContext);
        if (slotIndex < 1 || slotIndex > Capacity)
        {
            return false;
        }

        performContext.SlotIndex = slotIndex;
        return true;
    }

    private CardModel? FindCardInSlot(int slotIndex, CardModel excludedCard)
    {
        return PerformPile.Cards.FirstOrDefault(card =>
            card != excludedCard && CardContexts.GetOrCreate(card).SlotIndex == slotIndex);
    }

    private async Task<bool> MakeRoomForNewCard(CardModel cardModel, PerformContext performContext)
    {
        if (Capacity <= 0)
        {
            return false;
        }

        var occupiedCards = PerformPile.Cards
            .Where(card => card != cardModel)
            .Select(card => new
            {
                Card = card,
                Context = CardContexts.GetOrCreate(card)
            })
            .Where(entry => entry.Context.SlotIndex >= 1 && entry.Context.SlotIndex <= Capacity)
            .ToList();

        if (occupiedCards.Count < Capacity)
        {
            return false;
        }

        var squeezedCard = occupiedCards
            .OrderByDescending(entry => entry.Context.SlotIndex)
            .First();

        await MoveCardInternal(squeezedCard.Card);

        foreach (var entry in occupiedCards.Where(entry => entry.Card != squeezedCard.Card))
        {
            entry.Context.SlotIndex += 1;
        }

        performContext.SlotIndex = 1;
        PerformArea.RefreshItemLayout();
        return true;
    }

    private int FindAvailableSlot(CardModel cardModel, PerformContext performContext)
    {
        var occupiedSlots = PerformPile.Cards
            .Where(card => card != cardModel)
            .Select(card => CardContexts.GetOrCreate(card).SlotIndex)
            .Where(slotIndex => slotIndex is >= 1 and <= MaxCapacity)
            .ToHashSet();

        var aspirationSlot = NormalizeAspirationSlot(performContext.AspirationSlot);
        var candidates = performContext.Strategy switch
        {
            PerformEnqueueStrategy.Fixed => EnumerateNearbyFirst(aspirationSlot),
            PerformEnqueueStrategy.Top => EnumerateTopFirst(aspirationSlot),
            PerformEnqueueStrategy.Bottom => EnumerateBottomFirst(aspirationSlot),
            _ => EnumerateNearbyFirst(aspirationSlot)
        };

        foreach (var slotIndex in candidates)
        {
            if (slotIndex >= 1 && slotIndex <= Capacity && !occupiedSlots.Contains(slotIndex))
            {
                return slotIndex;
            }
        }

        return -1;
    }

    private int NormalizeAspirationSlot(int aspirationSlot)
    {
        return aspirationSlot < 1 ? 1 : Math.Min(aspirationSlot, Capacity);
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
        for (var slotIndex = aspirationSlot; slotIndex >= 1; slotIndex--)
        {
            yield return slotIndex;
        }

        for (var slotIndex = aspirationSlot + 1; slotIndex <= Capacity; slotIndex++)
        {
            yield return slotIndex;
        }
    }

    private IEnumerable<int> EnumerateBottomFirst(int aspirationSlot)
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

    public async Task AfterSecondaryResourceChanged(SecondaryResourceChangeContext ctx)
    {
        if (ctx.Player == _player && ctx.Definition.Id.Equals(BangDreamConst.LingeredResource))
        {
            if (ctx.NewAmount > 0 && ctx.NewAmount <= Capacity)
            {
                await TryPerformInternal(ctx.CombatState, ctx.NewAmount);
            }
        }
    }

    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(CardModel card,
        bool isAutoPlay, ResourceInfo resources, PileType pileType, CardPilePosition position)
    {
        if (card.Owner == Player && card is IPerformCard performanceCard && pileType == PileType.Discard)
        {
            return performanceCard.StopPerformanceNextPile();
        }

        return base.ModifyCardPlayResultPileTypeAndPosition(card, isAutoPlay, resources, pileType, position);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card is IPerformCard)
        {
            await Cmd.CustomScaledWait(0.35f, 0.5f);
        }
    }

    public override Task BeforeCombatStart()
    {
        if (!_isSubscribed)
        {
            _isSubscribed = true;
            PerformPile = BangDreamTools.GetPile(BangDreamConst.PerformPile, Player);

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
            CardContexts.Clear();
        }

        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        if (_isSubscribed)
        {
            _isSubscribed = false;
            PerformPile.CardAdded -= OnCardAdded;
            PerformPile.CardAddFinished -= OnCardAddFinished;
            PerformPile.CardRemoved -= OnCardRemoved;
            _cardsAwaitingArrival.Clear();
            CardContexts.Clear();
        }

        return Task.CompletedTask;
    }

    private static async Task MoveCardInternal(CardModel cardModel)
    {
        (PileType, CardPilePosition) nextPile;
        if (cardModel is IPerformCard performanceCard)
        {
            nextPile = performanceCard.StopPerformanceNextPile();
        }
        else
        {
            nextPile = (PileType.Discard, CardPilePosition.Bottom);
        }

        if (cardModel.IsDupe)
        {
            nextPile = (PileType.None, CardPilePosition.Bottom);
        }

        if (nextPile.Item1 == PileType.None)
        {
            await CardPileCmd.RemoveFromCombat(cardModel);
        }
        else
        {
            await CardPileCmd.Add(cardModel, nextPile.Item1, nextPile.Item2);
        }
    }

    private static void RunPerformAreaChangedHook(CardModel cardModel,
        Func<PlayerChoiceContext, CardModel, Task> handler)
    {
        if (cardModel.CombatState == null)
        {
            return;
        }

        TaskHelper.RunSafely(BangDreamHook.RunPerformHookAction(
            cardModel.CombatState,
            cardModel,
            choiceContext => handler(choiceContext, cardModel))
        );
    }
}
