using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Nodes;
using BangDreamLib.Scripts.Powers;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Power.Buff;

public class SetPerformancePower : BandPowerModel, ICardPerformanceHook
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    private int _extraCapacity;

    private NPerformanceManager? _manager;

    private readonly List<IPerformanceCard> _instantRollbacks = [];

    private readonly List<IPerformanceCard> _affected = [];

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            await PowerCmd.Decrement(this);
        }
    }

    public async Task OnCardEnterPerformanceArea(CardModel cardModel)
    {
        if (_manager != null && cardModel is IPerformanceCard performanceCard)
        {
            if (!_affected.Contains(performanceCard))
            {
                _affected.Add(performanceCard);
                if (performanceCard.IsInstant)
                {
                    performanceCard.IsInstant = false;
                    _instantRollbacks.Add(performanceCard);
                }

                var currentCount = _manager.ItemCount;
                while (currentCount > _manager.Capacity)
                {
                    if (await TryRemoveNonPerformanceCard())
                    {
                        currentCount--;
                    }
                    else
                    {
                        var needed = currentCount - _manager.Capacity;
                        _manager.AddCapacity(needed, true);
                        _extraCapacity += needed;
                        break;
                    }
                }
            }
        }
    }

    public async Task OnCardLeavePerformanceArea(CardModel cardModel)
    {
        if (cardModel is IPerformanceCard performanceCard && _affected.Contains(performanceCard))
        {
            var cardPile = BangDreamConst.PilePerformance.GetPile(Owner.Player!);
            if (!cardPile.Cards.Contains(cardModel))
            {
                Flash();
                await CardPileCmd.Add(cardModel, cardPile);
            }
        }
    }

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (Owner.Player == null)
        {
            await PowerCmd.Remove(this);
            return;
        }

        _manager = Owner.Player.AttachedNode().PerformanceManager;

        foreach (var cardModel in BangDreamConst.PilePerformance.GetPile(Owner.Player).Cards)
        {
            if (cardModel is IPerformanceCard performanceCard)
            {
                _affected.Add(performanceCard);
                if (performanceCard.IsInstant)
                {
                    performanceCard.IsInstant = false;
                    _instantRollbacks.Add(performanceCard);
                }
            }
        }
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        if (_manager != null)
        {
            if (_extraCapacity > 0)
            {
                _manager.ReduceCapacity(_extraCapacity);
            }

            foreach (var instantRollback in _instantRollbacks)
            {
                instantRollback.IsInstant = true;
                _affected.Remove(instantRollback);
                await _manager.RemoveItem((CardModel)instantRollback);
            }

            _instantRollbacks.Clear();
            _affected.Clear();
        }
    }

    private async Task<bool> TryRemoveNonPerformanceCard()
    {
        if (Owner.Player == null) return false;
        var pile = BangDreamConst.PilePerformance.GetPile(Owner.Player);
        var nonPerformance = pile.Cards.FirstOrDefault(c => c is not IPerformanceCard);
        if (nonPerformance == null) return false;

        if (nonPerformance.IsDupe)
        {
            await CardPileCmd.RemoveFromCombat(nonPerformance);
        }
        else
        {
            await CardPileCmd.Add(nonPerformance, PileType.Discard);
        }

        return true;
    }
}