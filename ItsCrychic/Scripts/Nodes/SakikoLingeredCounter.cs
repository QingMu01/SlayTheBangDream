using Godot;

namespace ItsCrychic.Scripts.Nodes;

public partial class SakikoLingeredCounter : LingeredCounter
{
    private Control? _counterImg;
    private Label? _counterText;


    public override void _Ready()
    {
        _counterImg = GetNode<Control>("%CounterImg");
        _counterText = GetNode<Label>("%CounterText");
    }



    protected override void OnEnergyChanged()
    {
        if (_counterText != null)
        {
            _counterText.Text = _counter!.Counter.ToString();
        }

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