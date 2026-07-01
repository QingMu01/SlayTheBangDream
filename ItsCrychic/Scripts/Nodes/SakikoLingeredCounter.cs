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
            var textureRects = _counterImg.GetChildren().OfType<TextureRect>().ToList();
            foreach (var textureRect in textureRects)
            {
                textureRect.Visible = false;
            }

            switch (_counter?.Counter)
            {
                case >= 7:
                    textureRects[6].Visible = true;
                    break;
                case >= 0:
                    textureRects[_counter.Counter].Visible = true;
                    break;
            }
        }
    }
}