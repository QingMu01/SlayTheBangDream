using Godot;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Nodes;

public partial class SakikoLingeredCounterSpecial : LingeredCounter
{
    private const double CounterTweenDuration = 0.35;
    private const string DarkStyle = "DarkMode";
    private const string LightStyle = "LightMode";

    private Control? _counterImg;
    private readonly Dictionary<int, TextureRect> _counterTextures = [];
    private Tween? _counterTween;
    private double _displayedCounter;
    private bool _hasDisplayedCounter;

    public override void _Ready()
    {
        _counterImg = GetNode<Control>("%CounterImg");
        LoadCounterTextures();

        var styleSeed = Player?.RunState.Rng.Seed ?? 3257999;
        var useDarkStyle = new Random((int)styleSeed).Next(99) < 50;

        foreach (var node in GetTree().GetNodesInGroup(useDarkStyle ? DarkStyle : LightStyle))
        {
            if (node is TextureRect textureRect)
            {
                textureRect.Visible = true;
            }
        }
        
        foreach (var node in GetTree().GetNodesInGroup(useDarkStyle ? LightStyle : DarkStyle))
        {
            if (node is TextureRect textureRect)
            {
                textureRect.Visible = false;
            }
        }
    }

    public override void _ExitTree()
    {
        _counterTween?.Kill();
        base._ExitTree();
    }

    protected override void OnEnergyChanged(SecondaryResourceChangedEvent changedEvent)
    {
        if (Player == null) return;

        AnimateCounter(changedEvent.NewAmount);
    }

    private void AnimateCounter(int targetCounter)
    {
        _counterTween?.Kill();

        if (!_hasDisplayedCounter)
        {
            _hasDisplayedCounter = true;
            UpdateDisplayedCounter(targetCounter);
            return;
        }

        if (Math.Abs(_displayedCounter - targetCounter) < 0.001)
        {
            UpdateDisplayedCounter(targetCounter);
            return;
        }

        var tween = CreateTween();
        _counterTween = tween;
        tween.TweenMethod(Callable.From<double>(UpdateDisplayedCounter), _displayedCounter, targetCounter,
                CounterTweenDuration)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        tween.Finished += () =>
        {
            if (_counterTween != tween) return;

            UpdateDisplayedCounter(targetCounter);
            _counterTween = null;
        };
    }

    private void UpdateDisplayedCounter(double counter)
    {
        _displayedCounter = counter;
        var roundedCounter = Math.Max(0, (int)Math.Round(counter));

        if (_counterImg != null)
        {
            foreach (var textureRect in _counterTextures.Values)
            {
                textureRect.Visible = false;
            }

            if (_counterTextures.TryGetValue(roundedCounter, out var texture))
            {
                texture.Visible = true;
            }
        }
    }

    private void LoadCounterTextures()
    {
        _counterTextures.Clear();
        if (_counterImg == null) return;

        foreach (var textureRect in _counterImg.GetChildren().OfType<TextureRect>())
        {
            if (TryGetCounterValue(textureRect.Name.ToString(), out var counter))
            {
                _counterTextures[counter] = textureRect;
            }
        }
    }

    private static bool TryGetCounterValue(string nodeName, out int counter)
    {
        counter = 0;
        var digitStart = nodeName.Length;
        while (digitStart > 0 && char.IsDigit(nodeName[digitStart - 1]))
        {
            digitStart--;
        }

        return digitStart < nodeName.Length && int.TryParse(nodeName[digitStart..], out counter);
    }
}