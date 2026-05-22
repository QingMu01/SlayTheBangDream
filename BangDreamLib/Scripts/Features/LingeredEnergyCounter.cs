using BangDreamLib.Scripts.Attributes;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace BangDreamLib.Scripts.Features;

[BangDreamIgnore]
public sealed class LingeredEnergyCounter : SingletonModel
{
    public event Action? OnEnergyChanged;

    public override bool ShouldReceiveCombatHooks => true;

    public const int MaxLingeredEnergy = 7;

    private int _counter;

    public int Counter
    {
        get => _counter;
        private set
        {
            AssertMutable();
            _counter = value;
        }
    }

    private Player? _owner;

    public Player Owner
    {
        get => _owner ?? throw new InvalidOperationException("LingeredEnergyCounter: Owner cannot be null.");
        set
        {
            AssertMutable();
            if (_owner == null)
            {
                _owner = value;
            }
            else
            {
                throw new InvalidOperationException("LingeredEnergyCounter: Owner cannot be set twice.");
            }
        }
    }

    public override Task BeforeCombatStart()
    {
        Counter = 0;
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        Counter = 0;
        return Task.CompletedTask;
    }

    public async Task AddLingeredEnergy(ICombatState combatState, int amount)
    {
        Counter += amount;
        await BangDreamHook.AfterLingeredEnergyAdded(combatState, Owner, amount);
        if (Counter >= MaxLingeredEnergy)
        {
            await BangDreamHook.OnLingeredEnergyFilled(combatState, Owner, Counter);
        }

        while (Counter > MaxLingeredEnergy)
        {
            var overflow = Counter - MaxLingeredEnergy;
            await BangDreamHook.FinalUnprocessedOverflow(combatState, Owner, overflow);
            Counter = MaxLingeredEnergy;
        }

        OnEnergyChanged?.Invoke();
    }

    public async Task ReduceLingeredEnergy(ICombatState combatState, int amount)
    {
        var originalLingeredEnergy = Counter;
        Counter -= amount;
        if (Counter < 0)
        {
            Counter = 0;
            await BangDreamHook.AfterLingeredEnergyReduced(combatState, Owner, originalLingeredEnergy);
        }
        else
        {
            await BangDreamHook.AfterLingeredEnergyReduced(combatState, Owner, amount);
        }

        OnEnergyChanged?.Invoke();
    }
}