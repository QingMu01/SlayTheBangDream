using BangDreamLib.Scripts.Features;
using Godot;

namespace ItsCrychic.Scripts.Nodes;

public abstract partial class LingeredCounter : Control
{
    protected LingeredEnergyCounter? _counter;

    public override void _ExitTree()
    {
        if (_counter != null)
        {
            _counter.OnEnergyChanged -= OnEnergyChanged;
        }
    }

    public void SetContext(LingeredEnergyCounter lingeredEnergyCounter)
    {
        if (_counter == null)
        {
            _counter = lingeredEnergyCounter;
            _counter.OnEnergyChanged += OnEnergyChanged;
            OnEnergyChanged();
        }
        else
        {
            throw new InvalidOperationException("lingered counter already set");
        }
    }

    protected abstract void OnEnergyChanged();
}