using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces;
using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Interfaces.GameHook;
using BangDreamInSpire.Scripts.Interfaces.Mechanics;
using BangDreamInSpire.Scripts.Mechanics.Perform.Schemes;
using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
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
using MegaCrit.Sts2.Core.Nodes.Cards;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;
using STS2RitsuLib.Networking.ManagedActions;
using STS2RitsuLib.Utils;

namespace BangDreamInSpire.Scripts.Mechanics.Perform;

/// <summary>管理歌单进出、演奏结算与网络同步；触发规则和布局由角色方案决定。</summary>
[RegisterSingleton]
public partial class PerformManager : HookedSingletonModel, IInCombatManager, IPerformService
{
    public PerformManager() : base(HookType.Combat)
    {
    }


    private enum PerformNetworkActionKind
    {
        Enter,
        Leave,
        Trigger,
        Chord
    }


    /// <summary>在同步战斗开始 Hook 内完成初始入队，不另排网络 Action。</summary>
    public async Task AddInitialCard(CardModel cardModel)
    {
        ArgumentNullException.ThrowIfNull(cardModel);
        var combatState = Player.Creature.CombatState;
        if (combatState == null) return;

        _initialPerformAreaAdditions.Add(cardModel);
        try
        {
            // 统一入口：带「插曲」附魔的牌从曲库放入歌单时改为进入手牌。
            await InsertSongRules.MoveIntoPerformArea(cardModel);
            if (cardModel.Pile != PerformPile) return;

            var context = CardContexts.GetOrCreate(cardModel);
            context.Manager = this;
            _pendingPerformAreaAdditions.Add(cardModel);
            await BangDreamHook.RunPerformHookAction(
                combatState,
                cardModel,
                choiceContext => HandleCardAddedInternal(choiceContext, cardModel, default));
        }
        finally
        {
            _initialPerformAreaAdditions.Remove(cardModel);
        }
    }

    private const string LocTable = "combat_messages";
    private const string MessagePrefix = "BANG_DREAM_IN_SPIRE_PERFORM_MANAGER";
    private const string ZeroCapacityPostfix = ".zero_capacity";
    private const string FullCapacityPostfix = ".full_capacity";

    private static readonly LocString EmptyThink = new(LocTable, MessagePrefix + ZeroCapacityPostfix);
    private static readonly LocString MaxSizeThink = new(LocTable, MessagePrefix + FullCapacityPostfix);

    public override bool ShouldReceiveCombatHooks => true;

    private Player? _player;
    private CardPile? _pile;

    /// <summary>注册单例未绑定玩家；所有 hook 必须先短路，再读取 Player。</summary>
    protected bool IsUnbound => _player == null;

    private readonly HashSet<CardModel> _cardsWithArrivalVisual = [];
    private readonly HashSet<CardModel> _cardsPendingArrival = [];

    /// <summary>逐牌观察入场状态的令牌，避免同帧入场与重复收口互相混淆。</summary>
    private readonly Dictionary<CardModel, int> _arrivalWatchTokens = [];

    private int _nextArrivalWatchToken;
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

    public IPerformArea? PerformArea { get; private set; }

    /// <summary>回填 UI 演奏区并提交当前方案。</summary>
    internal void AttachPerformArea(IPerformArea performArea)
    {
        PerformArea = performArea;
        performArea.SubmitScheme(_scheme);
        SyncPerformArea();
        performArea.SubmitChanged();
    }

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

        // CardAdded 早于 ContentsChanged，需提前登记 ID 供索引查询。
        NetCombatCardDb.Instance.IdCardForTesting(cardModel);

        if (Capacity > 0)
        {
            var context = CardContexts.GetOrCreate(cardModel);
            context.Manager = this;
            _pendingPerformAreaAdditions.Add(cardModel);
        }

        if (HasCardArrivalVisual(cardModel))
        {
            _cardsWithArrivalVisual.Add(cardModel);
            _cardsPendingArrival.Add(cardModel);
            var watchToken = ++_nextArrivalWatchToken;
            _arrivalWatchTokens[cardModel] = watchToken;
            TaskHelper.RunSafely(WatchCardArrivalAsync(cardModel, watchToken));
        }

        if (_initialPerformAreaAdditions.Contains(cardModel)) return;
        QueuePerformAreaChange(cardModel, PerformAreaChangeType.Added);
    }

    private static bool HasCardArrivalVisual(CardModel cardModel)
    {
        return NCard.FindOnTable(cardModel, PileType.Play) != null ||
               NCard.FindOnTable(cardModel, PileType.Hand) != null;
    }

    /// <summary>逐牌观察飞行结束；移动后静止两帧视为到达，无动画时 0.5 秒兜底。</summary>
    private async Task WatchCardArrivalAsync(CardModel cardModel, int watchToken)
    {
        try
        {
            if (Engine.GetMainLoop() is not SceneTree tree)
            {
                MarkCardArrived(cardModel, watchToken);
                return;
            }

            var moved = false;
            var stableFrames = 0;
            var elapsedFrames = 0;
            Vector2? lastPosition = NCard.FindOnTable(cardModel)?.GlobalPosition;
            // 观察预算比等待方少 10 帧，确保先收口。
            var framesLeft = PresentationWaitFrameBudget - 10;

            while (framesLeft-- > 0)
            {
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                elapsedFrames++;

                if (!IsArrivalWatchCurrent(cardModel, watchToken)) return;

                var cardNode = NCard.FindOnTable(cardModel);
                if (cardNode == null)
                {
                    // 首帧视觉可能尚未入桌，至少再观察一帧。
                    if (elapsedFrames == 1) continue;
                    MarkCardArrived(cardModel, watchToken);
                    return;
                }

                var position = cardNode.GlobalPosition;
                if (lastPosition is { } previous && !position.IsEqualApprox(previous))
                {
                    moved = true;
                    stableFrames = 0;
                }
                else
                {
                    stableFrames++;
                }

                lastPosition = position;

                // 必须先发生移动，避免误判静止手牌。
                if (moved && stableFrames >= 2)
                {
                    MarkCardArrived(cardModel, watchToken);
                    return;
                }

                if (!moved && framesLeft <= PresentationWaitFrameBudget - 40)
                {
                    MarkCardArrived(cardModel, watchToken);
                    return;
                }
            }

            MarkCardArrived(cardModel, watchToken);
        }
        catch (Exception ex)
        {
            BangDreamInSpireCore.Logger.Warn(
                $"Card arrival watcher for {cardModel.Id.Entry} failed: {ex}");
            MarkCardArrived(cardModel, watchToken);
        }
    }

    private bool IsArrivalWatchCurrent(CardModel cardModel, int watchToken)
    {
        return _arrivalWatchTokens.TryGetValue(cardModel, out var current) &&
               current == watchToken &&
               _cardsPendingArrival.Contains(cardModel);
    }

    private void MarkCardArrived(CardModel cardModel, int watchToken)
    {
        if (!IsArrivalWatchCurrent(cardModel, watchToken)) return;

        _arrivalWatchTokens.Remove(cardModel);
        _cardsPendingArrival.Remove(cardModel);
        _cardsWithArrivalVisual.Remove(cardModel);

        if (cardModel.Pile == PerformPile)
        {
            PerformArea?.PlayCardArrivalBounce(cardModel);
        }

        QueuePerformAreaChange(cardModel, PerformAreaChangeType.Arrived);
    }

    private void CancelArrivalWatch(CardModel cardModel)
    {
        _arrivalWatchTokens.Remove(cardModel);
    }

    private void OnCardRemoved(CardModel cardModel)
    {
        if (!CombatManager.Instance.IsInProgress)
            return;

        // 再次入场需重新触发入场效果。
        _instantPerformedCards.Remove(cardModel);
        PerformArea?.RemoveItem(cardModel);
        CancelArrivalWatch(cardModel);
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
            where !pendingAdditions.Contains(cardModel) && !IsValidSlot(performContext.SlotIndex)
            select cardModel;

        foreach (var overflowItem in overflowItems.ToList())
        {
            await Cmd.CustomScaledWait(0.15f, 0.3f);
            await MoveCardInternal(overflowItem);
        }
    }

    private async Task HandleCardAddedInternal(
        PlayerChoiceContext choiceContext,
        CardModel cardModel,
        PerformEnqueueRequest request)
    {
        ArgumentNullException.ThrowIfNull(cardModel.CombatState);

        var performContext = CardContexts.GetOrCreate(cardModel);
        performContext.Manager = this;

        if (Capacity == 0)
        {
            performContext.Request = default;
            ThinkCmd.Play(EmptyThink, Player.Creature, 1.5f);
            if (PerformPile.Cards.Contains(cardModel)) await MoveCardInternal(cardModel);
            return;
        }

        var enqueuePlan = CreateEnqueuePlan(cardModel, request);
        if (enqueuePlan == null)
        {
            performContext.Request = default;
            ThinkCmd.Play(MaxSizeThink, Player.Creature, 1.5f);
            if (PerformPile.Cards.Contains(cardModel)) await MoveCardInternal(cardModel);
            return;
        }

        try
        {
            await ApplyEnqueuePlan(enqueuePlan, performContext);

            if (!_cardsPendingArrival.Contains(cardModel) && PerformPile.Cards.Contains(cardModel))
            {
                PerformArea?.AddItem(cardModel, performContext, _cardsWithArrivalVisual.Contains(cardModel));
            }

            await BangDreamHook.OnCardTriggeredPerform(choiceContext, cardModel.CombatState, cardModel);

            await BangDreamHook.OnCardEnterPerformArea(choiceContext, cardModel.CombatState, cardModel, cardModel);

            await RemoveOverflowItems();
        }
        finally
        {
            CompletePendingPerformAreaAddition(cardModel);

            // 落位覆盖仅对本次入场有效。
            performContext.Request = default;
        }
    }

    private Task HandleCardArrivedInternal(CardModel cardModel)
    {
        CancelArrivalWatch(cardModel);
        _cardsPendingArrival.Remove(cardModel);

        if (!PerformPile.Cards.Contains(cardModel) || cardModel.CombatState == null) return Task.CompletedTask;

        var performContext = CardContexts.GetOrCreate(cardModel);
        if (performContext.Manager != this || !IsValidSlot(performContext.SlotIndex)) return Task.CompletedTask;

        PerformArea?.AddItem(cardModel, performContext, waitForCardArrival: false);
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

    /// <summary>演奏指定槽位中的非即兴牌。</summary>
    public async Task PerformSlot(
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

            BangDreamInSpireCore.Logger.Info(
                $"Player {Player.Character} ({Player.NetId}) Perform : {slotCard.Title}");
        }
    }

    /// <summary>奏响掩码覆盖分组内的全部已落位牌；空掩码表示全部。</summary>
    public async Task PerformChordSlots(PerformChord? chord = null, PlayerChoiceContext? choiceContext = null)
    {
        var mask = chord ?? PerformChord.None;
        var slotIndices = CollectChordSlotIndices(mask);
        BangDreamInSpireCore.Logger.Info(
            $"Player {Player.Character} ({Player.NetId}) Chord perform {mask} : " +
            $"slots [{string.Join(", ", slotIndices)}]");

        foreach (var slotIndex in slotIndices)
        {
            await PerformSlot(slotIndex, false, choiceContext);
        }
    }

    /// <summary>按卡牌实际槽位返回掩码覆盖的索引，升序去重。</summary>
    private List<int> CollectChordSlotIndices(PerformChord mask)
    {
        return PerformPile.Cards
            .Select(card => CardContexts.GetOrCreate(card).SlotIndex)
            .Where(slotIndex => IsValidSlot(slotIndex) && _capacity.IsSlotInMask(slotIndex, mask))
            .Distinct()
            .OrderBy(slotIndex => slotIndex)
            .ToList();
    }

    /// <summary>入场表现完成后即兴演奏，每次入场至多一次。</summary>
    public async Task TryInstant(CardModel cardModel, PlayerChoiceContext? choiceContext = null)
    {
        ArgumentNullException.ThrowIfNull(cardModel.CombatState);

        if (cardModel is IPerformCard { IsInstant: true } performCard)
        {
            if (!_instantPerformedCards.Add(cardModel))
            {
                return;
            }

            await PerformCard(cardModel, performCard, true, choiceContext);

            BangDreamInSpireCore.Logger.Info(
                $"Player {Player.Character} ({Player.NetId}) Instant Perform : {cardModel.Title}");
        }
    }

    /// <summary>请求槽位演奏；冻结时改为本地激活所属分组。</summary>
    public void RequestSlotPerform(int slotIndex, bool isSubsideTriggered)
    {
        if (TryActivateGroupsWhileFrozen(ResolveGroupOfSlot(slotIndex), $"slot {slotIndex}"))
        {
            return;
        }

        RequestPerformAction(new PerformNetworkActionPayload(
            PerformNetworkActionKind.Trigger,
            null,
            slotIndex,
            isSubsideTriggered,
            (int)PerformChord.None));
    }

    /// <summary>请求和弦演奏；冻结时改为本地激活掩码覆盖的分组。</summary>
    public void RequestChordPerform(PerformChord chord)
    {
        if (TryActivateGroupsWhileFrozen(
                chord == PerformChord.None ? PerformChord.All : chord,
                $"chord {chord}"))
        {
            return;
        }

        // 和弦掩码包含目标，槽位字段仅用于 Trigger。
        RequestPerformAction(new PerformNetworkActionPayload(
            PerformNetworkActionKind.Chord,
            null,
            0,
            false,
            (int)chord));
    }

    public async Task PerformCard(CardModel cardModel, bool isAutoPerform = false)
    {
        if (cardModel is IPerformCard performCard)
        {
            // 单牌演奏在冻结时激活所属分组。
            if (TryActivateGroupsWhileFrozen(ResolveGroupOfCard(cardModel), $"card {cardModel.Title}"))
            {
                return;
            }

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

        if (!await WaitForCardPresentation(cardModel)) return;

        PlayPerformFlashVfx(cardModel);

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

    /// <summary>飞牌到达由动画回调驱动，不依赖演奏 Action 完成。</summary>
    /// <summary>等待入场与布局动画，最多 MaxWaitFrames，避免表现异常阻塞同步网络动作。</summary>
    private const int PresentationWaitFrameBudget = 120;

    private async Task<bool> WaitForCardPresentation(CardModel cardModel)
    {
        if (PerformArea is not { } area || !area.IsInsideTree()) return true;

        var framesLeft = PresentationWaitFrameBudget;

        while (_cardsPendingArrival.Contains(cardModel))
        {
            if (!CombatManager.Instance.IsInProgress || cardModel.Pile != PerformPile ||
                !area.IsInsideTree()) return false;
            if (--framesLeft < 0) return AbandonStuckPresentation(cardModel);
            await area.AwaitProcessFrame();
        }

        if (!CombatManager.Instance.IsInProgress || cardModel.Pile != PerformPile ||
            !area.IsInsideTree()) return false;

        var context = CardContexts.GetOrCreate(cardModel);
        area.AddItem(cardModel, context, waitForCardArrival: false);
        // 等 Container 帧末布局后再核对动画。
        await area.AwaitProcessFrame();
        while (CombatManager.Instance.IsInProgress && cardModel.Pile == PerformPile && area.IsInsideTree())
        {
            var slot = context.Slot;
            if (slot == null || slot.Model != cardModel) return false;
            if (!slot.IsArrivalVisualPlaying && !area.IsSlotLayoutPlaying) return true;
            if (--framesLeft < 0) return AbandonStuckPresentation(cardModel);
            await area.AwaitProcessFrame();
        }

        return false;
    }

    /// <summary>超时告警并清除等待标记，让结算继续。</summary>
    private bool AbandonStuckPresentation(CardModel cardModel)
    {
        BangDreamInSpireCore.Logger.Warn(
            $"Card presentation for {cardModel.Id.Entry} did not settle within " +
            $"{PresentationWaitFrameBudget} frames; continuing without waiting for it.");

        CancelArrivalWatch(cardModel);
        _cardsPendingArrival.Remove(cardModel);
        return true;
    }

    private void PlayPerformFlashVfx(CardModel cardModel)
    {
        CardContexts.GetOrCreate(cardModel).Slot?.SuppressPortraitSweep();
        PerformArea?.PlayFlashVfx(cardModel);
    }

    /// <summary>重排歌单并重新触发入场效果。</summary>
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

        // 连续单分组才能重写为 1 基索引，并校验当前容量。
        if (orderedCards.Count > _capacity.ActiveSlotCount)
        {
            throw new InvalidOperationException(
                $"Cannot reorder {orderedCards.Count} cards into {_capacity.ActiveSlotCount} active perform slots.");
        }

        // 重排后允许即兴牌再次入场演奏。
        foreach (var card in orderedCards)
        {
            _instantPerformedCards.Remove(card);
        }

        for (var index = 0; index < orderedCards.Count; index++)
        {
            var context = CardContexts.GetOrCreate(orderedCards[index]);
            context.Manager = this;
            context.SlotIndex = index + 1;
        }

        PerformArea?.RefreshItemLayout();

        foreach (var card in orderedCards)
        {
            if (card.Pile != PerformPile || card.CombatState == null) continue;

            await BangDreamHook.OnCardTriggeredPerform(null, card.CombatState, card);
            await BangDreamHook.OnCardEnterPerformArea(choiceContext, card.CombatState, card, card);
        }
    }

    public int GetExpectedSlotIndex(CardModel cardModel)
    {
        return CreateEnqueuePlan(cardModel, CardContexts.GetOrCreate(cardModel).Request)?.SlotIndex ?? -1;
    }

    /// <inheritdoc />
    public int GetSlotIndex(CardModel cardModel)
    {
        return CardContexts.GetOrCreate(cardModel).SlotIndex;
    }

    /// <inheritdoc />
    public void InheritSlot(CardModel source, CardModel target)
    {
        var sourceContext = CardContexts.GetOrCreate(source);
        var targetContext = CardContexts.GetOrCreate(target);
        targetContext.Manager = this;
        targetContext.SlotIndex = sourceContext.SlotIndex;
    }

    /// <inheritdoc />
    public void RemoveCardContext(CardModel cardModel)
    {
        CardContexts.Remove(cardModel);
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
            PerformArea?.RefreshItemLayout();
            foreach (var change in plan.SlotChanges)
            {
                change.Context.Slot?.PlayPortraitReveal();
            }
        }
    }

    /// <summary>按组内序号规划入队，再映射到全局槽位；手动请求只覆盖目标分组或槽位，
    /// 失效时回退到卡牌自身规则。</summary>
    private EnqueuePlan? CreateEnqueuePlan(CardModel incomingCard, PerformEnqueueRequest request = default)
    {
        if (Capacity <= 0) return null;

        var incomingContext = CardContexts.GetOrCreate(incomingCard);

        if (request.SlotIndex is { } requestedSlot && IsValidSlot(requestedSlot))
        {
            var occupant = PerformPile.Cards
                .Where(card => card != incomingCard)
                .Select(card => new OccupiedSlot(card, CardContexts.GetOrCreate(card)))
                .FirstOrDefault(slot => slot.Context.SlotIndex == requestedSlot);
            return new EnqueuePlan(requestedSlot, occupant?.Card, []);
        }

        if (IsValidSlot(incomingContext.SlotIndex))
        {
            return new EnqueuePlan(incomingContext.SlotIndex, null, []);
        }

        var groupSlots = _capacity.GetActiveSlots(request.Group ?? _capacity.ResolveDefaultGroup());
        if (groupSlots.Count == 0) return null;

        var groupSlotSet = groupSlots.ToHashSet();
        var occupiedSlots = PerformPile.Cards
            .Where(card => card != incomingCard)
            .Select(card => new OccupiedSlot(card, CardContexts.GetOrCreate(card)))
            .Where(slot => groupSlotSet.Contains(slot.Context.SlotIndex))
            .ToDictionary(slot => slot.Context.SlotIndex);

        var aspirationLocal = ToLocalOrdinal(groupSlots, incomingContext.AspirationSlot);

        if (incomingContext.Strategy == PerformEnqueueStrategy.Fixed && aspirationLocal > 0)
        {
            var aspirationSlot = groupSlots[aspirationLocal - 1];
            occupiedSlots.TryGetValue(aspirationSlot, out var displacedSlot);
            return new EnqueuePlan(aspirationSlot, displacedSlot?.Card, []);
        }

        if (incomingContext.Strategy is not (PerformEnqueueStrategy.Default or PerformEnqueueStrategy.Fixed))
        {
            var clampedLocal = Math.Clamp(aspirationLocal, 1, groupSlots.Count);
            var availableLocal = EnumerateCandidateSlots(incomingContext.Strategy, clampedLocal, groupSlots.Count)
                .FirstOrDefault(local => !occupiedSlots.ContainsKey(groupSlots[local - 1]));
            if (availableLocal > 0)
            {
                return new EnqueuePlan(groupSlots[availableLocal - 1], null, []);
            }
        }

        var firstAvailableLocal = Enumerable.Range(1, groupSlots.Count)
            .FirstOrDefault(local => !occupiedSlots.ContainsKey(groupSlots[local - 1]));
        if (firstAvailableLocal > 0)
        {
            var targetSlot = groupSlots[firstAvailableLocal - 1];
            var slotChanges = occupiedSlots.Values
                .Where(slot => slot.Context.SlotIndex < targetSlot)
                .Select(slot => new SlotChange(slot.Context, ToNextSlot(groupSlots, slot.Context.SlotIndex)))
                .ToList();
            return new EnqueuePlan(groupSlots[0], null, slotChanges);
        }

        var lastSlot = groupSlots[^1];
        occupiedSlots.TryGetValue(lastSlot, out var overflowSlot);
        var overflowSlotChanges = occupiedSlots.Values
            .Where(slot => slot.Context.SlotIndex < lastSlot)
            .Select(slot => new SlotChange(slot.Context, ToNextSlot(groupSlots, slot.Context.SlotIndex)))
            .ToList();
        return new EnqueuePlan(groupSlots[0], overflowSlot?.Card, overflowSlotChanges);
    }

    /// <summary>返回组内 1 基序号，不属于该组时为 0。</summary>
    private static int ToLocalOrdinal(IReadOnlyList<int> groupSlots, int slotIndex)
    {
        for (var index = 0; index < groupSlots.Count; index++)
        {
            if (groupSlots[index] == slotIndex)
            {
                return index + 1;
            }
        }

        return 0;
    }

    private static int ToNextSlot(IReadOnlyList<int> groupSlots, int slotIndex)
    {
        var localOrdinal = ToLocalOrdinal(groupSlots, slotIndex);
        return localOrdinal >= 1 && localOrdinal < groupSlots.Count ? groupSlots[localOrdinal] : slotIndex;
    }

    private bool IsValidSlot(int slotIndex)
    {
        return _capacity.IsValidSlot(slotIndex);
    }

    private static IEnumerable<int> EnumerateCandidateSlots(
        PerformEnqueueStrategy strategy,
        int aspirationSlot,
        int slotCount)
    {
        return strategy switch
        {
            PerformEnqueueStrategy.Bottom => EnumerateBottomFirst(aspirationSlot, slotCount),
            PerformEnqueueStrategy.Top => EnumerateTopFirst(aspirationSlot, slotCount),
            _ => EnumerateNearbyFirst(aspirationSlot, slotCount)
        };
    }

    private static IEnumerable<int> EnumerateNearbyFirst(int aspirationSlot, int slotCount)
    {
        yield return aspirationSlot;

        for (var offset = 1; offset < slotCount; offset++)
        {
            var lower = aspirationSlot - offset;
            if (lower >= 1)
            {
                yield return lower;
            }

            var upper = aspirationSlot + offset;
            if (upper <= slotCount)
            {
                yield return upper;
            }
        }
    }

    private static IEnumerable<int> EnumerateTopFirst(int aspirationSlot, int slotCount)
    {
        for (var slotIndex = aspirationSlot; slotIndex <= slotCount; slotIndex++)
        {
            yield return slotIndex;
        }

        for (var slotIndex = aspirationSlot - 1; slotIndex >= 1; slotIndex--)
        {
            yield return slotIndex;
        }
    }

    private static IEnumerable<int> EnumerateBottomFirst(int aspirationSlot, int slotCount)
    {
        for (var slotIndex = aspirationSlot; slotIndex >= 1; slotIndex--)
        {
            yield return slotIndex;
        }

        for (var slotIndex = aspirationSlot + 1; slotIndex <= slotCount; slotIndex++)
        {
            yield return slotIndex;
        }
    }

    // 本体 0.113 起 SingletonModel 是 RunModel，注册的单例会同时以「局内模型单例」和
    // 「RitsuLib 战斗钩子订阅」两种身份进入 CombatState.IterateHookListeners；这两份都没绑定玩家，
    // 读 Player 会抛 Owner is not Initialized 并让整次出牌失败。真正的每玩家结算由
    // PerformMechanic.GetCombatHookModels 注册的那份克隆负责（见 SubmitCombatState 的 _player）。
    // 新增 override 一律先用 IsUnbound 短路。
    public override CardLocation ModifyCardPlayResultLocation(CardModel card, bool isAutoPlay, ResourceInfo resources,
        CardLocation cardLocation)
    {
        if (IsUnbound)
        {
            return cardLocation;
        }

        if (cardLocation.player == Player && card is IPerformCard performanceCard &&
            cardLocation.pileType == PileType.Discard)
        {
            return performanceCard.StopPerformanceNextPile();
        }

        return cardLocation;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        // 每个经理都会收到全部出牌事件；只有出牌者的经理需要为「入场表现」等待一次，
        // 否则 N 人联机每次音乐牌出牌会叠加 N 份延迟。
        if (IsUnbound || play.Card.Owner != Player)
        {
            return;
        }

        if (play.Card is IPerformCard)
        {
            await Cmd.CustomScaledWait(0.35f, 0.5f);
        }
    }

    public void SubmitCombatState()
    {
        PerformPile = BangDreamConst.PerformPile.GetPile(Player);

        _scheme = Player.Character is IPerformableCharacter character
            ? character.CreatePerformScheme()
            : EmptyPerformScheme.Instance;
        _capacity = new PerformCapacityState(_scheme);
        // 分组激活位是跨战斗复用的缓存（演奏区重建时由 SetActivatedGroups 重新投喂），
        // 必须在本场开始前清空，否则上一场的掩码会残留到下一场。
        _activeGroups = PerformChord.None;

        if (PerformArea != null)
        {
            PerformArea.SubmitScheme(_scheme);
            SyncPerformArea();
            PerformArea.SubmitChanged();
        }

        PerformPile.CardAdded += OnCardAdded;
        PerformPile.CardRemoved += OnCardRemoved;
        _arrivalWatchTokens.Clear();
        _cardsWithArrivalVisual.Clear();
        _cardsPendingArrival.Clear();
        _initialPerformAreaAdditions.Clear();
        ClearPerformAreaChanges();
        _instantPerformedCards.Clear();
        CardContexts.Clear();
    }

    public void UnsubscribeCombatState()
    {
        // 每次战斗都会重建演奏区节点，这里清掉引用，下一场由 PerformAreaMechanic 重新回填。
        PerformArea = null;
        PerformPile.CardAdded -= OnCardAdded;
        PerformPile.CardRemoved -= OnCardRemoved;
        _arrivalWatchTokens.Clear();
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
        var isEnter = type == PerformAreaChangeType.Added;
        var request = isEnter ? CardContexts.GetOrCreate(cardModel).Request : default;
        var payload = new PerformNetworkActionPayload(
            isEnter ? PerformNetworkActionKind.Enter : PerformNetworkActionKind.Leave,
            index,
            0,
            false,
            (int)PerformChord.None,
            request.SlotIndex ?? 0,
            request.Group is { } group ? (int)group : 0);
        if (!RitsuLibManagedNetActions.Request(null, PerformNetworkAction, payload, Player.NetId))
            TaskHelper.RunSafely(HandleRejectedNetworkAction(payload));
    }

    private Task HandlePerformAreaChange(
        PlayerChoiceContext choiceContext,
        PerformAreaChange change,
        PerformEnqueueRequest request)
    {
        return change.Type switch
        {
            PerformAreaChangeType.Added => HandleCardAddedInternal(choiceContext, change.CardModel, request),
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