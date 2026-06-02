using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Nodes;
using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Combat;
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

    private const int MaxCapacity = 7;

    public override bool ShouldReceiveCombatHooks => true;

    private bool _isSubscribed;
    private Player? _owner;
    private CardPile? _performancePile;

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
        get => _performancePile ??
               throw new InvalidOperationException("PerformanceManager: PerformancePile cannot be null.");
        set
        {
            AssertMutable();
            _performancePile = value;
        }
    }

    public NPerformanceArea? PerformanceArea { get; private set; }

    public int Capacity { get; private set; }

    public event Action? RefreshLayout;

    public void AddCapacity(int amount)
    {
        if (amount <= 0) return;
        Capacity += amount;
        if (Capacity > MaxCapacity)
        {
            Capacity = MaxCapacity;
            ThinkCmd.Play(MaxSizeThink, Player.Creature, 1.5d);
        }

        RefreshLayout?.Invoke();
    }

    public void ReduceCapacity(int amount)
    {
        if (amount <= 0) return;
        Capacity = Math.Max(0, Capacity - amount);

        var overflow = PerformancePile.Cards.Count - Capacity;
        if (overflow > 0)
            _ = RemoveOverflowItems(overflow);

        RefreshLayout?.Invoke();
    }

    private void OnCardAdded(CardModel cardModel)
    {
        _ = HandleCardAdded(cardModel);
    }

    private void OnCardRemoved(CardModel cardModel)
    {
        RefreshLayout?.Invoke();
    }

    private async Task RemoveOverflowItems(int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (PerformancePile.Cards.Count == 0) break;
            await HandleCardRemoval(PerformancePile.Cards[0]);
        }
    }

    private async Task HandleCardAdded(CardModel cardModel)
    {
        if (!CombatManager.Instance.IsInProgress)
            return;
        if (cardModel.CombatState == null)
        {
            BangDreamLibCore.Logger.Error("card must be add to combat first!");
            return;
        }

        var isInstant = cardModel is IPerformanceCard { IsInstant: true };
        if (!isInstant && Capacity == 0)
        {
            ThinkCmd.Play(EmptyThink, Player.Creature, 1.5d);
            await HandleCardRemoval(cardModel);
            return;
        }

        if (cardModel is IPerformanceCard performanceCard)
        {
            await performanceCard.OnStartPerformance(new HookPlayerChoiceContext(cardModel, cardModel.Owner.NetId,
                cardModel.CombatState!, GameActionType.Combat));
        }

        await BangDreamHook.OnCardEnterPerformanceArea(cardModel.CombatState, cardModel);


        if (isInstant)
        {
            await HandleCardRemoval(cardModel);
        }
        else
        {
            var overflow = PerformancePile.Cards.Count - Capacity;
            if (overflow > 0)
                await RemoveOverflowItems(overflow);
        }

        RefreshLayout?.Invoke();
    }

    private async Task HandleCardRemoval(CardModel? card)
    {
        if (card == null || !PerformancePile.Cards.Contains(card)) return;
        var isDupe = card.IsDupe;
        var nextPile = PileType.Discard;
        if (card is IPerformanceCard performanceCard)
        {
            nextPile = performanceCard.StopPerformanceNextPile;

            await performanceCard.OnStopPerformance(new HookPlayerChoiceContext(card, card.Owner.NetId,
                card.CombatState!, GameActionType.Combat));
        }

        await BangDreamHook.OnCardLeavePerformanceArea(card.CombatState!, card);

        RefreshLayout?.Invoke();

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

    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(CardModel card,
        bool isAutoPlay,
        ResourceInfo resources, PileType pileType, CardPilePosition position)
    {
        if (card is IPerformanceCard { StopPerformanceNextPile: not PileType.Discard } performanceCard
            && pileType == PileType.Discard)
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
            PerformancePile = BangDreamConst.PilePerformance.GetPile(Player);
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