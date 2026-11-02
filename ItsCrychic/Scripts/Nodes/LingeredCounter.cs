using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Nodes;

public abstract partial class LingeredCounter : Control
{
    private CombatManager? _combatManager;
    private CombatStateTracker? _stateTracker;

    protected Player? Player;

    public override void _EnterTree()
    {
        if (CombatManager.Instance is not { } combatManager)
        {
            return;
        }

        _combatManager = combatManager;
        combatManager.CombatBegan += TryBindPlayer;
        if (combatManager.StateTracker is { } stateTracker)
        {
            _stateTracker = stateTracker;
            stateTracker.CombatStateChanged += TryBindPlayer;
        }
    }

    public override void _ExitTree()
    {
        Unsubscribe();
        if (Player != null)
        {
            SecondaryResourceStateStore.Get(Player).Changed -= OnEnergyChanged;
        }
    }

    public void SetPlayer(Player player)
    {
        if (Player == player)
        {
            return;
        }

        if (Player != null)
        {
            throw new InvalidOperationException("lingered counter already set");
        }

        Player = player;
        SecondaryResourceStateStore.Get(Player).Changed += OnEnergyChanged;
        OnPlayerBound(player);
    }

    protected abstract void OnEnergyChanged(SecondaryResourceChangedEvent changedEvent);

    protected virtual void OnPlayerBound(Player player)
    {
    }

    private void TryBindPlayer(CombatState state)
    {
        if (Player != null)
        {
            Unsubscribe();
            return;
        }

        if (LocalContext.GetMe(state) is not { } player)
        {
            return;
        }

        Unsubscribe();
        SetPlayer(player);
    }

    private void Unsubscribe()
    {
        if (_combatManager != null)
        {
            _combatManager.CombatBegan -= TryBindPlayer;
            _combatManager = null;
        }

        if (_stateTracker != null)
        {
            _stateTracker.CombatStateChanged -= TryBindPlayer;
            _stateTracker = null;
        }
    }
}
