using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Nodes;
using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace BangDreamLib.Scripts.Features;

public class PerformanceManager : SingletonModel
{
    private static readonly LocString EmptyThink = new("combat_messages", "BANG_DREAM_LIB_FILL_PERFORMANCE_PILE.empty");

    private static readonly LocString MaxSizeThink =
        new("combat_messages", "BANG_DREAM_LIB_MAX_SIZE_PERFORMANCE_PILE.filled");

    private const int MaxCapacity = 10;

    public event Func<CardModel, Task>? CardEnteredPerformance;
    public event Func<CardModel, Task>? CardLeftPerformance;
    public event Func<int, Task>? CapacityChanged;

    public override bool ShouldReceiveCombatHooks => true;

    private bool _isSubscribed;

    private Player? _owner;
    private CardPile? _pile;

    public Player Player
    {
        get => _owner ?? throw new InvalidOperationException("PerformanceManager: Owner cannot be null.");
        set
        {
            AssertMutable();
            if (_owner == null)
            {
                _owner = value;
            }
            else
            {
                throw new InvalidOperationException("PerformanceManager: Owner cannot be set twice.");
            }
        }
    }

    public CardPile PerformancePile
    {
        get => _pile ?? throw new InvalidOperationException("PerformanceManager: PerformancePile cannot be null.");
        set
        {
            AssertMutable();
            _pile = value;
        }
    }

    public NPerformanceArea? PerformanceArea { get; private set; }

    public int Capacity { get; private set; }

    public void AddCapacity(int amount)
    {
        if (amount <= 0) return;
        if (Capacity == MaxCapacity)
        {
            ThinkCmd.Play(MaxSizeThink, Player.Creature, 1.5d);
            return;
        }

        Capacity = Math.Min(MaxCapacity, Capacity + amount);

        CapacityChanged?.Invoke(Capacity);
    }

    public void ReduceCapacity(int amount)
    {
        if (amount <= 0) return;
        Capacity = Math.Max(0, Capacity - amount);

        RemoveOverflowItems(false).ContinueWith(_ => CapacityChanged?.Invoke(Capacity));
    }

    private void OnCardAdded(CardModel cardModel)
    {
        _ = HandleCardAddedInternal(cardModel);
    }

    private void OnCardRemoved(CardModel cardModel)
    {
        _ = HandleCardRemovedInternal(cardModel).ContinueWith(_ => CardLeftPerformance?.Invoke(cardModel));
    }

    private async Task RemoveOverflowItems(bool squeezeMode = true)
    {
        while (PerformancePile.Cards.Count > Capacity)
        {
            await Cmd.Wait(0.15f);
            if (squeezeMode)
                await MoveCardInternal(PerformancePile.Cards[0]);
            else
                await MoveCardInternal(PerformancePile.Cards[^1]);
        }
    }

    private async Task HandleCardAddedInternal(CardModel cardModel)
    {
        if (!PerformancePile.Cards.Contains(cardModel)) return;
        var isInstant = cardModel is IPerformanceCard { IsInstant: true };
        if (!isInstant && Capacity == 0)
        {
            ThinkCmd.Play(EmptyThink, Player.Creature, 1.5f);
            await MoveCardInternal(cardModel);
            return;
        }

        if (cardModel is IPerformanceCard performanceCard)
        {
            await performanceCard.OnStartPerformance(new HookPlayerChoiceContext(cardModel, cardModel.Owner.NetId,
                cardModel.CombatState!, GameActionType.Combat));
        }

        await BangDreamHook.OnCardEnterPerformanceArea(cardModel.CombatState!, cardModel);
        if (CardEnteredPerformance != null)
        {
            await CardEnteredPerformance.Invoke(cardModel);
        }

        if (isInstant)
        {
            await Cmd.Wait(0.8f);
            await HandleCardRemovedInternal(cardModel);
        }
        else
        {
            await RemoveOverflowItems();
        }
    }

    private async Task HandleCardRemovedInternal(CardModel card)
    {
        if (PerformancePile.Cards.Contains(card))
        {
            await MoveCardInternal(card);
        }

        if (card is IPerformanceCard performanceCard)
        {
            await performanceCard.OnStopPerformance(new HookPlayerChoiceContext(card, card.Owner.NetId,
                card.CombatState!, GameActionType.Combat));
        }

        await BangDreamHook.OnCardLeavePerformanceArea(card.CombatState!, card);
    }

    private static async Task MoveCardInternal(CardModel cardModel)
    {
        PileType nextPile;
        if (cardModel is IPerformanceCard performanceCard)
        {
            nextPile = performanceCard.StopPerformanceNextPile;
        }
        else
        {
            nextPile = PileType.Discard;
        }

        if (cardModel.IsDupe)
        {
            nextPile = PileType.None;
        }

        switch (nextPile)
        {
            case PileType.Discard:
            {
                await CardCmd.Discard(new BlockingPlayerChoiceContext(), cardModel);
                break;
            }
            case PileType.None:
            {
                await CardPileCmd.RemoveFromCombat(cardModel);
                break;
            }
            default:
            {
                await CardPileCmd.Add(cardModel, nextPile);
                break;
            }
        }
    }

    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(CardModel card,
        bool isAutoPlay, ResourceInfo resources, PileType pileType, CardPilePosition position)
    {
        if (card.Owner == Player &&
            card is IPerformanceCard { StopPerformanceNextPile: not PileType.Discard } performanceCard &&
            pileType == PileType.Discard)
        {
            return (performanceCard.StopPerformanceNextPile, position);
        }

        return base.ModifyCardPlayResultPileTypeAndPosition(card, isAutoPlay, resources, pileType, position);
    }

    public override Task BeforeCombatStart()
    {
        if (!_isSubscribed)
        {
            _isSubscribed = true;
            PerformancePile = BangDreamTools.GetPile(BangDreamConst.PerformanceTable, Player);
            if (Player.Character is IPerformanceCharacter character)
                Capacity = character.GetDefaultCapacity;
            else
                Capacity = 0;

            PerformancePile.CardAdded += OnCardAdded;
            PerformancePile.CardRemoved += OnCardRemoved;

            var creatureNode = Player.Creature.GetCreatureNode();
            if (creatureNode != null)
            {
                var offset = creatureNode.GlobalPosition > creatureNode.GetViewport().GetVisibleRect().GetCenter() / 2f
                    ? new Vector2(creatureNode.Hitbox.Size.X / 2f + 48f, 48f)
                    : new Vector2(creatureNode.Hitbox.Size.X / 2f - 48f, -48f);

                PerformanceArea = NPerformanceArea.Create(this, offset);
                creatureNode.AddChildSafely(PerformanceArea);
            }
            else
            {
                BangDreamLibCore.Logger.Warn(
                    $"PerformanceArea node create failed: Player({Player.Character}) creatureNode is null.");
            }
        }

        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        if (_isSubscribed)
        {
            _isSubscribed = false;
            PerformancePile.CardAdded -= OnCardAdded;
            PerformancePile.CardRemoved -= OnCardRemoved;
        }

        return Task.CompletedTask;
    }
}
