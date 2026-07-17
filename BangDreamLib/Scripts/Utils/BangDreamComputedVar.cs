using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace BangDreamLib.Scripts.Utils;

public class BangDreamComputedVar : DynamicVar
{
    private readonly Func<ComputedVarsContext, decimal> _currentValueFactory;
    private readonly Func<ComputedVarsContext, CardPreviewMode, bool, decimal>? _previewValueFactory;

    public BangDreamComputedVar(
        string name,
        decimal baseValue,
        Func<ComputedVarsContext, decimal> currentValueFactory,
        Func<ComputedVarsContext, CardPreviewMode, bool, decimal>? previewValueFactory = null)
        : base(name, baseValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(currentValueFactory);

        _currentValueFactory = currentValueFactory;
        _previewValueFactory = previewValueFactory;
    }

    public decimal Calculate(Creature? target = null)
    {
        return _currentValueFactory(new ComputedVarsContext(_owner as CardModel, target, BaseValue));
    }

    public override void UpdateCardPreview(
        CardModel card,
        CardPreviewMode previewMode,
        Creature? target,
        bool runGlobalHooks)
    {
        var computedVarsContext = new ComputedVarsContext(card, target, BaseValue);
        PreviewValue = _previewValueFactory?.Invoke(computedVarsContext, previewMode, runGlobalHooks) ??
                       _currentValueFactory(computedVarsContext);
    }

    protected override decimal GetBaseValueForIConvertible()
    {
        return Calculate();
    }

    public override string ToString()
    {
        return Calculate().ToString();
    }

    public record ComputedVarsContext(CardModel? Card, Creature? Target, decimal BaseValue)
    {
        public CardModel ActiveCard =>
            Card ?? throw new InvalidOperationException("Card is null");

        public IRunState ActiveRunState =>
            ActiveCard.RunState ?? throw new InvalidOperationException("RunState is null");

        public ICombatState ActiveCombatState =>
            ActiveCard.CombatState ?? throw new InvalidOperationException("CombatState is null");

        public bool IsInCombat()
        {
            if (Card is { IsMutable: true })
            {
                var runState = Card.RunState;
                var combatState = Card.CombatState;

                return runState != null && runState is not NullRunState &&
                       combatState != null && combatState is not NullCombatState;
            }

            return false;
        }
    }
}