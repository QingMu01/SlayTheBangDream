using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Nodes;

public abstract partial class LingeredCounter : Control
{
    protected Player? Player;

    public override void _ExitTree()
    {
        if (Player != null)
        {
            SecondaryResourceStateStore.Get(Player).Changed -= OnEnergyChanged;
        }
    }

    public void SetPlayer(Player player)
    {
        if (Player == null)
        {
            Player = player;
            SecondaryResourceStateStore.Get(Player).Changed += OnEnergyChanged;
        }
        else
        {
            throw new InvalidOperationException("lingered counter already set");
        }
    }

    protected abstract void OnEnergyChanged(SecondaryResourceChangedEvent changedEvent);
}