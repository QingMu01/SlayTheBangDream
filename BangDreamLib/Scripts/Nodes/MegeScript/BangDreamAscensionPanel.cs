using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Saves;

namespace BangDreamLib.Scripts.Nodes.MegeScript;

public partial class BangDreamAscensionPanel : Control
{
    private static readonly StringName TabLeftHotkey = MegaInput.viewDeckAndTabLeft;
    private static readonly StringName TabRightHotkey = MegaInput.viewExhaustPileAndTabRight;
    private static readonly StringName FontOutlineTheme = "font_outline_color";
    private static readonly StringName H = new("h");
    private static readonly StringName S = new("s");
    private static readonly StringName V = new("v");
    private static readonly Color RedLabelOutline = new("593400");
    private static readonly Color BlueLabelOutline = new("004759");

    [Signal]
    public delegate void AscensionLevelChangedEventHandler();

    private int _maxAscension;

    private BangDreamGoldArrowButton? _leftArrow;
    private BangDreamGoldArrowButton? _rightArrow;
    private Label? _ascensionLevel;
    private MegaRichTextLabel? _info;
    private TextureRect? _leftTriggerIcon;
    private TextureRect? _rightTriggerIcon;

    private ShaderMaterial? _iconHsv;

    private bool _arrowsVisible = true;

    private MultiplayerUiMode _mode = MultiplayerUiMode.Singleplayer;

    private Tween? _tween;

    public int Ascension { get; private set; }

    public override void _Ready()
    {
        _leftTriggerIcon = GetNode<TextureRect>("%LeftTriggerIcon");
        _rightTriggerIcon = GetNode<TextureRect>("%RightTriggerIcon");

        _leftArrow = GetNode<BangDreamGoldArrowButton>("HBoxContainer/LeftArrowContainer/LeftArrow");
        _rightArrow = GetNode<BangDreamGoldArrowButton>("HBoxContainer/RightArrowContainer/RightArrow");

        _ascensionLevel = GetNode<Label>("HBoxContainer/AscensionIconContainer/AscensionIcon/AscensionLevel");

        _info = GetNode<MegaRichTextLabel>("HBoxContainer/AscensionDescription/Description");
        _iconHsv = (ShaderMaterial)GetNode<Control>("%AscensionIcon").Material;

        _leftArrow.Connect(NClickableControl.SignalName.Released,
            Callable.From<NButton>(delegate { DecrementAscension(); }));
        _rightArrow.Connect(NClickableControl.SignalName.Released,
            Callable.From<NButton>(delegate { IncrementAscension(); }));

        NControllerManager.Instance?.Connect(NControllerManager.SignalName.MouseDetected,
            Callable.From(UpdateControllerButton));
        NControllerManager.Instance?.Connect(NControllerManager.SignalName.ControllerDetected,
            Callable.From(UpdateControllerButton));

        NInputManager.Instance?.Connect(NInputManager.SignalName.InputRebound,
            Callable.From(UpdateControllerButton));

        UpdateControllerButton();
    }

    public void Initialize(MultiplayerUiMode mode)
    {
        _mode = mode;
        switch (_mode)
        {
            case MultiplayerUiMode.Host:
                SetFireBlue();
                _arrowsVisible = true;
                SetMaxAscension(SaveManager.Instance.Progress.MaxMultiplayerAscension);
                SetAscensionLevel(Math.Min(_maxAscension, SaveManager.Instance.Progress.PreferredMultiplayerAscension));
                NHotkeyManager.Instance?.PushHotkeyPressedBinding(TabLeftHotkey, DecrementAscension);
                NHotkeyManager.Instance?.PushHotkeyPressedBinding(TabRightHotkey, IncrementAscension);
                break;
            case MultiplayerUiMode.Singleplayer:
                SetFireRed();
                _arrowsVisible = true;
                SetMaxAscension(0);
                SetAscensionLevel(0);
                NHotkeyManager.Instance?.PushHotkeyPressedBinding(TabLeftHotkey, DecrementAscension);
                NHotkeyManager.Instance?.PushHotkeyPressedBinding(TabRightHotkey, IncrementAscension);
                break;
            case MultiplayerUiMode.None:
            case MultiplayerUiMode.Client:
            case MultiplayerUiMode.Load:
            default:
            {
                if ((uint)(_mode - 3) <= 1u)
                {
                    SetFireBlue();
                    _arrowsVisible = false;
                    SetMaxAscension(0);
                }

                break;
            }
        }
    }

    private void SetFireBlue()
    {
        _iconHsv?.SetShaderParameter(H, 0.52f);
        _iconHsv?.SetShaderParameter(S, 0.75f);
        _iconHsv?.SetShaderParameter(V, 1.2f);
        _ascensionLevel?.AddThemeColorOverride(FontOutlineTheme, BlueLabelOutline);
    }

    private void SetFireRed()
    {
        _iconHsv?.SetShaderParameter(H, 1.0f);
        _iconHsv?.SetShaderParameter(S, 0.75f);
        _iconHsv?.SetShaderParameter(V, 1.2f);
        _ascensionLevel?.AddThemeColorOverride(FontOutlineTheme, RedLabelOutline);
    }

    public void Cleanup()
    {
        if ((uint)(_mode - 1) <= 1u)
        {
            NHotkeyManager.Instance?.RemoveHotkeyPressedBinding(TabLeftHotkey, DecrementAscension);
            NHotkeyManager.Instance?.RemoveHotkeyPressedBinding(TabRightHotkey, IncrementAscension);
        }
    }

    public void SetAscensionLevel(int ascension)
    {
        if (Ascension != ascension)
        {
            Ascension = ascension;
            EmitSignal(SignalName.AscensionLevelChanged);
        }

        RefreshAscensionText();
        RefreshArrowVisibility();
    }

    private void IncrementAscension()
    {
        if (Ascension < _maxAscension)
        {
            SetAscensionLevel(Ascension + 1);
        }

        BangDreamLibCore.Logger.Info($"Ascension changed to {Ascension}");
    }

    private void DecrementAscension()
    {
        if (Ascension > 0)
        {
            SetAscensionLevel(Ascension - 1);
        }

        BangDreamLibCore.Logger.Info($"Ascension changed to {Ascension}");
    }

    private void RefreshArrowVisibility()
    {
        if (_leftArrow != null) _leftArrow.Visible = _arrowsVisible && Ascension != 0;
        if (_rightArrow != null) _rightArrow.Visible = _arrowsVisible && Ascension != _maxAscension;
    }

    public void SetMaxAscension(int maxAscension)
    {
        BangDreamLibCore.Logger.Info($"Max ascension changed to {maxAscension}");
        _maxAscension = maxAscension;
        if (Ascension >= _maxAscension)
        {
            SetAscensionLevel(_maxAscension);
        }

        Visible = _maxAscension > 0;
        RefreshArrowVisibility();
    }

    private void RefreshAscensionText()
    {
        if (_ascensionLevel != null) _ascensionLevel.Text = Ascension.ToString();

        if (_info != null)
        {
            var formattedText = AscensionHelper.GetTitle(Ascension).GetFormattedText();
            var formattedText2 = AscensionHelper.GetDescription(Ascension).GetFormattedText();
            _info.Text = "[b][gold]" + formattedText + "[/gold][/b]\n" + formattedText2;
        }
    }

    public void AnimIn()
    {
        if (Visible)
        {
            var modulate = Modulate;
            modulate.A = 0f;
            Modulate = modulate;
            _tween?.FastForwardToCompletion();
            _tween = CreateTween();
            _tween.TweenProperty(this, "modulate:a", 1f, 0.2);
        }
    }

    private void UpdateControllerButton()
    {
        if (Visible)
        {
            if ((uint)(_mode - 1) <= 1u)
            {
                if (_leftTriggerIcon != null)
                    _leftTriggerIcon.Visible = NControllerManager.Instance?.IsUsingController ?? false;
                if (_rightTriggerIcon != null)
                    _rightTriggerIcon.Visible = NControllerManager.Instance?.IsUsingController ?? false;
                if (_leftTriggerIcon != null)
                    _leftTriggerIcon.Texture = NInputManager.Instance?.GetHotkeyIcon(MegaInput.viewDeckAndTabLeft);
                if (_rightTriggerIcon != null)
                    _rightTriggerIcon.Texture =
                        NInputManager.Instance?.GetHotkeyIcon(MegaInput.viewExhaustPileAndTabRight);
            }
            else
            {
                if (_leftTriggerIcon != null) _leftTriggerIcon.Visible = false;
                if (_rightTriggerIcon != null) _rightTriggerIcon.Visible = false;
            }
        }
    }
}