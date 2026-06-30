using Godot;

namespace ItsCrychic.Scripts.Nodes;

public partial class SakikoLingeredCounterSpecial : LingeredCounter
{
    private const string DarkStyle = "DarkMode";
    private const string LightStyle = "LightMode";

    private Control? _counterImg;

    public override void _Ready()
    {
        _counterImg = GetNode<Control>("%CounterImg");

        var styleSeed = _counter?.Owner.RunState.Rng.Seed ?? 3257999;
        var useDarkStyle = new Random((int)styleSeed).Next(99) < 50;

        foreach (var node in GetTree().GetNodesInGroup(useDarkStyle ? DarkStyle : LightStyle))
        {
            if (node is TextureRect textureRect)
            {
                textureRect.Visible = true;
            }
        }
    }

    protected override void OnEnergyChanged()
    {
        if (_counterImg != null)
        {
            var textureRects = (from textureRect in _counterImg.GetChildren()
                where textureRect is TextureRect
                select textureRect).ToList();
            foreach (var textureRect in textureRects)
            {
                ((TextureRect)textureRect).Visible = false;
            }

            switch (_counter?.Counter)
            {
                case >= 7:
                    ((TextureRect)textureRects[6]).Visible = true;
                    break;
                case > 0:
                    ((TextureRect)textureRects[_counter.Counter - 1]).Visible = true;
                    break;
            }
        }
    }
}