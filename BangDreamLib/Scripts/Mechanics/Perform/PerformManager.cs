using System.Text.Json;
using BangDreamLib.Scripts.Enums;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Mechanics.Perform.Schemes;
using BangDreamLib.Scripts.Nodes;
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
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;
using STS2RitsuLib.Utils;

namespace BangDreamLib.Scripts.Mechanics.Perform;

/// <summary>
/// 歌单管理器。负责歌单的进出规则、演奏一次的结算与网络同步，并提供演奏 API。
/// 不决定"何时演奏"（由角色的 <see cref="IPerformTriggerListener" /> 规则发起）、
/// 也不决定"有多少容量、如何摆放"（由角色的 <see cref="IPerformScheme" /> 提供）。
/// </summary>
public class PerformManager : SingletonModel, IInCombatManager
{
    private enum PerformNetworkActionKind
    {
        Enter,
        Leave,
        Trigger,
        Chord
    }

    private sealed record PerformNetworkActionPayload(
        PerformNetworkActionKind Kind,
        uint? CardIndex,
        int SlotIndex,
        bool IsSubsideTriggered,
        int ChordMask,
        int RequestSlotIndex = 0,
        int RequestGroupMask = 0);

    private static readonly RitsuLibManagedNetActionDescriptor<PerformNetworkActionPayload> PerformNetworkAction = new(
        "BangDreamLib", "perform_lifecycle",
        static payload => JsonSerializer.SerializeToUtf8Bytes(payload),
        static bytes => JsonSerializer.Deserialize<PerformNetworkActionPayload>(bytes) ??
                        throw new InvalidOperationException("Invalid perform lifecycle payload."),
        static context => ExecuteNetworkPerformAction(context), GameActionType.Combat);

    /// <summary>本生命周期动作的稳定操作码，供补丁识别；未注册时为 0。</summary>
    internal static ulong PerformOpcode { get; private set; }

    /// <summary>
    /// 还原 payload 中携带的落位覆盖。槽位优先级高于分组，两者都为空时表示按卡牌自身规则入队。
    /// </summary>
    private static PerformEnqueueRequest ToEnqueueRequest(PerformNetworkActionPayload payload)
    {
        if (payload.RequestSlotIndex >= 1)
        {
            return PerformEnqueueRequest.Slot(payload.RequestSlotIndex);
        }

        return payload.RequestGroupMask != 0
            ? PerformEnqueueRequest.ForGroup((PerformChord)payload.RequestGroupMask)
            : default;
    }

    internal static void InitializeNetwork()
    {
        PerformOpcode = RitsuLibManagedNetActions.Register(PerformNetworkAction);
    }

    /// <summary>该 Action 是否为歌单生命周期动作（供 <c>IsGameActionPlayerDriven</c> 补丁使用）。</summary>
    internal static bool IsPerformLifecycleAction(GameAction action)
    {
        return PerformOpcode != 0 &&
               action is RitsuLibManagedGameAction managed &&
               managed.DescriptorOpcode == PerformOpcode;
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
                choiceContext => HandleCardAddedInternal(choiceContext, cardModel, default));
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
            await PerformSlot(payload.SlotIndex, payload.IsSubsideTriggered, choiceContext);
            return;
        }

        if (payload.Kind == PerformNetworkActionKind.Chord)
        {
            await PerformChordSlots((PerformChord)payload.ChordMask, choiceContext);
            return;
        }

        if (!payload.CardIndex.HasValue) return;
        var card = NetCombatCard.ForTesting(payload.CardIndex.Value).ToCardModelOrNull();
        if (card == null) return;
        var change = new PerformAreaChange(card,
            payload.Kind == PerformNetworkActionKind.Enter
                ? PerformAreaChangeType.Added
                : PerformAreaChangeType.Removed);
        await HandlePerformAreaChange(choiceContext, change, ToEnqueueRequest(payload));
    }

    private async Task ExecuteLocalPerformAction(PerformNetworkActionPayload payload)
    {
        var combatState = Player.Creature.CombatState;
        if (combatState == null) return;
        if (payload.Kind == PerformNetworkActionKind.Trigger)
        {
            await PerformSlot(payload.SlotIndex, payload.IsSubsideTriggered);
            return;
        }

        if (payload.Kind == PerformNetworkActionKind.Chord)
        {
            await PerformChordSlots((PerformChord)payload.ChordMask);
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
            combatState, card, choiceContext => HandlePerformAreaChange(choiceContext, change, ToEnqueueRequest(payload)));
    }

    private const string LocTable = "combat_messages";
    private const string MessagePrefix = "BANG_DREAM_LIB_PERFORM_MANAGER";
    private const string ZeroCapacityPostfix = ".zero_capacity";
    private const string FullCapacityPostfix = ".full_capacity";
    private const string PerformFlashVfxPath = "res://BangDreamLib/scenes/vfx/perform_flash_vfx.tscn";

    private static readonly LocString EmptyThink = new(LocTable, MessagePrefix + ZeroCapacityPostfix);
    private static readonly LocString MaxSizeThink = new(LocTable, MessagePrefix + FullCapacityPostfix);

    public override bool ShouldReceiveCombatHooks => true;

    private Player? _player;
    private CardPile? _pile;

    private IPerformScheme _scheme = EmptyPerformScheme.Instance;
    private PerformCapacityState _capacity = new(EmptyPerformScheme.Instance);

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

    /// <summary>当前已激活的演奏槽位总数。</summary>
    public int Capacity => _capacity.ActiveSlotCount;

    /// <summary>全部演奏分组的容量上限之和。</summary>
    public int MaxCapacity => _capacity.TotalSlotCount;

    /// <summary>角色提供的演奏方案（不可变策略）。</summary>
    public IPerformScheme Scheme => _scheme;

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

    /// <summary>
    /// 增加演奏容量。未指定和弦分组时对全部分组生效。
    /// </summary>
    public void AddCapacity(int amount, PerformChord? chord = null)
    {
        if (amount <= 0) return;

        switch (_capacity.Add(amount, chord))
        {
            case PerformCapacityChangeResult.Applied:
                SyncPerformArea();
                break;
            case PerformCapacityChangeResult.RejectedAtMax:
                ThinkCmd.Play(MaxSizeThink, Player.Creature, 1.5d);
                break;
            case PerformCapacityChangeResult.Ignored:
            default:
                break;
        }
    }

    /// <summary>
    /// 减少演奏容量。未指定和弦分组时对全部分组生效。
    /// </summary>
    public void ReduceCapacity(int amount, PerformChord? chord = null)
    {
        if (amount <= 0) return;
        _capacity.Reduce(amount, chord);
        SyncPerformArea();
        TaskHelper.RunSafely(RemoveOverflowItems());
    }

    private void SyncPerformArea()
    {
        PerformArea.SyncSlots(_capacity.TotalSlotCount, _capacity.ActiveSlots);
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

        // 离开歌单即视为本次入场结束：再次进入时应重新触发即兴演奏与入场效果。
        _instantPerformedCards.Remove(cardModel);
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
                PerformArea.AddItem(cardModel, performContext, _cardsWithArrivalVisual.Contains(cardModel));

            await BangDreamHook.OnCardTriggeredPerform(choiceContext, cardModel.CombatState, cardModel);

            await BangDreamHook.OnCardEnterPerformArea(choiceContext, cardModel.CombatState, cardModel, cardModel);

            await RemoveOverflowItems();
        }
        finally
        {
            CompletePendingPerformAreaAddition(cardModel);

            // 落位覆盖是一次性的：本张牌已处理完毕，避免残留到下一次入场。
            performContext.Request = default;
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

    /// <summary>
    /// 演奏指定全局槽位中的卡牌。仅当该槽位存在非即兴牌时生效。
    /// </summary>
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

            BangDreamLibCore.Logger.Info(
                $"Player {Player.Character} ({Player.NetId}) Perform : {slotCard.Title}");
        }
    }

    /// <summary>
    /// 奏响指定和弦掩码覆盖的全部分组中的**所有卡牌**：逐槽位奏响这些分组内已落位卡牌所在的槽位，
    /// 因此同一分组的每张牌都会奏响（即兴牌由 <see cref="TryInstant" /> 负责，不在此列）。
    /// 传入 <see langword="null" /> 或 <see cref="PerformChord.None" /> 时覆盖全部分组。
    /// </summary>
    public async Task PerformChordSlots(PerformChord? chord = null, PlayerChoiceContext? choiceContext = null)
    {
        var mask = chord ?? PerformChord.None;
        var slotIndices = CollectChordSlotIndices(mask);
        BangDreamLibCore.Logger.Info(
            $"Player {Player.Character} ({Player.NetId}) Chord perform {mask} : " +
            $"slots [{string.Join(", ", slotIndices)}]");

        foreach (var slotIndex in slotIndices)
        {
            await PerformSlot(slotIndex, false, choiceContext);
        }
    }

    /// <summary>
    /// 取掩码覆盖分组内已落位卡牌的槽位索引，升序去重。
    /// 以"卡牌实际所在的槽位"为准，而非按当前容量枚举槽位，保证分组内的每张牌都被覆盖。
    /// </summary>
    private List<int> CollectChordSlotIndices(PerformChord mask)
    {
        return PerformPile.Cards
            .Select(card => CardContexts.GetOrCreate(card).SlotIndex)
            .Where(slotIndex => IsValidSlot(slotIndex) && _capacity.IsSlotInMask(slotIndex, mask))
            .Distinct()
            .OrderBy(slotIndex => slotIndex)
            .ToList();
    }

    /// <summary>
    /// 让指定卡牌立即"即兴"演奏。同一张牌在一次入场内只即兴一次。
    /// </summary>
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

            BangDreamLibCore.Logger.Info(
                $"Player {Player.Character} ({Player.NetId}) Instant Perform : {cardModel.Title}");
        }
    }

    /// <summary>
    /// 发起一次槽位演奏请求。单机或请求被拒时在本地执行。
    /// </summary>
    public void RequestSlotPerform(int slotIndex, bool isSubsideTriggered)
    {
        RequestPerformAction(new PerformNetworkActionPayload(
            PerformNetworkActionKind.Trigger,
            null,
            slotIndex,
            isSubsideTriggered,
            (int)PerformChord.None));
    }

    /// <summary>
    /// 发起一次和弦演奏请求：奏响该和弦覆盖的每个分组中的全部卡牌。
    /// 单机或请求被拒时在本地执行。
    /// </summary>
    public void RequestChordPerform(PerformChord chord)
    {
        // 和弦演奏不使用槽位字段，目标分组完全由 ChordMask 决定（SlotIndex 仅供 Trigger 种类使用）。
        RequestPerformAction(new PerformNetworkActionPayload(
            PerformNetworkActionKind.Chord,
            null,
            0,
            false,
            (int)chord));
    }

    private void RequestPerformAction(PerformNetworkActionPayload payload)
    {
        if (!CombatManager.Instance.IsInProgress) return;
        if (!LocalContext.IsMe(Player)) return;

        if (!RitsuLibManagedNetActions.Request(null, PerformNetworkAction, payload, Player.NetId))
            TaskHelper.RunSafely(HandleRejectedNetworkAction(payload));
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

    private void PlayPerformFlashVfx(CardModel cardModel)
    {
        if (!PerformArea.IsInsideTree()) return;
        if (!PerformArea.TryGetCardSlotCenter(cardModel, out var slotCenter)) return;

        var vfx = BangDreamPreloadManager.GetScene(PerformFlashVfxPath).Instantiate<PerformFlashVfx>();
        vfx.FlashColor = _scheme.GetSlotColor(CardContexts.GetOrCreate(cardModel).SlotIndex, cardModel);
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

        // 重排视为"重新加入歌单"：清空本次入场的即兴标记，使即兴牌重新演奏。
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

        PerformArea.RefreshItemLayout();

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

    /// <summary>
    /// 开始一次手动落位选择：按目标类型计算候选槽位，并让本地演奏区域进入选择态。
    /// 非手动落位类型、手柄方向导航、无候选槽位（容量为 0 或演奏区域未就绪）、
    /// 指定分组但只有一个可达分组时都不进入选择态，出牌按卡牌自身的入队规则处理。
    /// </summary>
    public void BeginManualSlotSelection(CardModel cardModel)
    {
        ArgumentNullException.ThrowIfNull(cardModel);

        var mode = PerformTargetTypes.ResolveMode(cardModel.TargetType);
        if (mode == PerformSlotSelectionMode.None) return;
        if (NControllerManager.Instance?.IsUsingDirectionalNavigation == true)
        {
            BangDreamLibCore.Logger.Info(
                $"Manual slot selection skipped for {cardModel.Id.Entry}: controller navigation.");
            return;
        }

        if (!TryGetPerformArea(out var performArea))
        {
            BangDreamLibCore.Logger.Warn(
                $"Manual slot selection skipped for {cardModel.Id.Entry}: perform area is not ready.");
            return;
        }

        var candidates = BuildSlotCandidates();
        if (candidates.Count == 0)
        {
            BangDreamLibCore.Logger.Warn(
                $"Manual slot selection skipped for {cardModel.Id.Entry}: no active slot candidate.");
            return;
        }

        // 指定分组模式下只有一个可达分组时无从选择，其落位等价于常规规则，直接不进入选择态。
        if (mode == PerformSlotSelectionMode.Group &&
            candidates.Select(candidate => candidate.Group).Distinct().Count() <= 1)
        {
            BangDreamLibCore.Logger.Info(
                $"Manual slot selection skipped for {cardModel.Id.Entry}: only one reachable group.");
            return;
        }

        performArea.BeginSlotSelection(cardModel, mode, candidates);
        BangDreamLibCore.Logger.Info(
            $"Manual slot selection started for {cardModel.Id.Entry} ({mode}) : " +
            $"slots [{string.Join(", ", candidates.Select(candidate => candidate.SlotIndex))}]");
    }

    /// <summary>
    /// 按当前鼠标位置立即刷新一次选择指向（松手瞬间判定用）。
    /// </summary>
    public void RefreshManualSlotSelection(CardModel cardModel)
    {
        if (TryGetPerformArea(out var performArea) && performArea.SlotSelectionCard == cardModel)
        {
            performArea.RefreshSlotSelection();
        }
    }

    /// <summary>
    /// 该牌的手动落位选择当前是否指向候选槽位（判定前先按当前鼠标位置刷新一次指向）。
    /// </summary>
    public bool HasManualSlotCandidate(CardModel cardModel)
    {
        if (!TryGetPerformArea(out var performArea) || performArea.SlotSelectionCard != cardModel) return false;

        performArea.RefreshSlotSelection();
        return performArea.HoveredSlotIndex > 0;
    }

    /// <summary>该卡当前是否正处于手动落位选择态（未进入选择态时出牌走常规规则）。</summary>
    public bool IsManualSlotSelectionActive(CardModel cardModel)
    {
        return TryGetPerformArea(out var performArea) &&
               performArea.IsSlotSelectionActive &&
               performArea.SlotSelectionCard == cardModel;
    }

    /// <summary>
    /// 结束手动落位选择并取出结果；返回 <see langword="false" /> 表示未命中任何候选槽位。
    /// 命中时把结果写入该牌的 <see cref="PerformContext.Request" />，供入队规划与网络动作读取。
    /// </summary>
    public bool TryConsumeManualSlotSelection(CardModel cardModel, out PerformEnqueueRequest request)
    {
        request = default;
        if (!IsManualSlotSelectionActive(cardModel)) return false;

        var selected = PerformArea.TryConsumeSlotSelection();
        if (selected.IsEmpty) return false;

        CardContexts.GetOrCreate(cardModel).Request = selected;
        request = selected;
        return true;
    }

    /// <summary>
    /// 结束选择态与槽位高亮。<paramref name="cardModel" /> 非空时只结束该牌自己的选择：
    /// 其他卡牌出牌收尾不应打断正在进行的落位选择。
    /// </summary>
    public void EndManualSlotSelection(CardModel? cardModel = null)
    {
        if (!TryGetPerformArea(out var performArea) || !performArea.IsSlotSelectionActive) return;
        if (cardModel != null && performArea.SlotSelectionCard != cardModel) return;

        performArea.EndSlotSelection();
    }

    /// <summary>取消手动落位选择，并清空该牌尚未消费的落位请求。</summary>
    public void CancelManualSlotSelection(CardModel cardModel)
    {
        EndManualSlotSelection(cardModel);
        CardContexts.GetOrCreate(cardModel).Request = default;
    }

    /// <summary>选择态诊断文本；该牌不在选择态时返回占位说明。</summary>
    public string DescribeManualSlotSelection(CardModel cardModel)
    {
        return TryGetPerformArea(out var performArea) && performArea.SlotSelectionCard == cardModel
            ? performArea.DescribeSlotSelection()
            : "<no active selection for this card>";
    }

    /// <summary>本角色全部已激活槽位及其所属和弦分组，作为手动选择的候选集合。</summary>
    private List<PerformSlotCandidate> BuildSlotCandidates()
    {
        // 分组和弦为 PerformChord.None 表示"唯一的默认分组"：其落位与常规规则等价，
        // 且无法用和弦表达落位请求，因此多分组方案下不作为候选（留待常规规则处理）。
        var skipDefaultGroup = _scheme.Groups.Count > 1;
        var candidates = new List<PerformSlotCandidate>();
        foreach (var descriptor in _scheme.Groups)
        {
            if (skipDefaultGroup && descriptor.Chord == PerformChord.None) continue;

            foreach (var slotIndex in _capacity.GetActiveSlots(descriptor.Chord))
            {
                candidates.Add(new PerformSlotCandidate(slotIndex, descriptor.Chord));
            }
        }

        return candidates;
    }

    private bool TryGetPerformArea(out NPerformArea performArea)
    {
        performArea = PerformArea;
        return GodotObject.IsInstanceValid(performArea);
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

    /// <summary>
    /// 为入队卡牌生成本次入队规划。槽位算法按"目标分组的组内序号"运作，
    /// 再映射回全局槽位索引，因此单分组（祥子）与多分组（睦）共用同一套规则。
    /// 目标分组取自玩家的手动请求，或方案默认分组；卡牌和弦不参与入组判定。
    /// </summary>
    /// <param name="incomingCard">本次入场的卡牌。</param>
    /// <param name="request">
    /// 玩家手动指定的落位覆盖：指定槽位时强制进入该槽位并顶掉占用者；指定分组时只替换分组，
    /// 其余规则不变。槽位在规划时已失效（例如容量被削减）时回退到卡牌自身规则。
    /// </param>
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

    /// <summary>全局槽位索引在组内的 1 基序号；不在该组内时返回 0。</summary>
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

    /// <summary>组内上移一格后的全局槽位索引；已在组末时保持原值。</summary>
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

        _scheme = Player.Character is IPerformableCharacter character
            ? character.CreatePerformScheme()
            : EmptyPerformScheme.Instance;
        _capacity = new PerformCapacityState(_scheme);

        PerformArea.SubmitScheme(_scheme);
        SyncPerformArea();
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

    private Task HandleRejectedNetworkAction(PerformNetworkActionPayload payload)
    {
        if (RunManager.Instance.NetService.Type == NetGameType.Singleplayer)
            return ExecuteLocalPerformAction(payload);

        BangDreamLibCore.Logger.Error(
            $"Perform action was rejected by the Sidecar action queue: player={Player.NetId} kind={payload.Kind}.");
        return Task.CompletedTask;
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