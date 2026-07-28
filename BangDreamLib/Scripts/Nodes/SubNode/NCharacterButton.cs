using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using STS2RitsuLib.Ui.Toast;

namespace BangDreamLib.Scripts.Nodes.SubNode;

public partial class NCharacterButton : Button
{
    private const string MessagePrefixKey = "BANG_DREAM_LIB_CHARACTER_CANT_SELECT_";
    private const float AnimationDuration = 0.12f;

    private static readonly Vector2 ActiveOffset = new(50f, 0f);

    private BangDreamCharacterSelector? _parent;

    private Tween? _hoverTween;
    private TextureRect? _icon;

    private Vector2 _originalPosition;
    private bool _isSelected;

    public CharacterModel? Character { get; private set; }

    public bool IsSelected
    {
        get => _isSelected;
        private set
        {
            if (Character is IGroupableCharacter aggregationCharacter)
            {
                _isSelected = value && aggregationCharacter.AllowSelect;
                return;
            }

            _isSelected = value;
        }
    }

    public static NCharacterButton Create(BangDreamCharacterSelector selector, CharacterModel character)
    {
        var button = PreloadKey.CharacterButton.GetScene().Instantiate<NCharacterButton>();
        button.Character = character;
        button._parent = selector;
        return button;
    }

    public override void _Ready()
    {
        SelfModulate = Character?.NameColor ?? Colors.Black;
        _originalPosition = Position;
        _icon = GetNode<TextureRect>("Icon");

        if (Character is IBangDreamMateData { SelectLogo: { } path } && !string.IsNullOrWhiteSpace(path))
        {
            _icon.Texture = BangDreamPreloadManager.GetTexture2D(path);
        }

        ApplyVisualState(IsSelected);
    }

    public override void _EnterTree()
    {
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
        Pressed += OnPressed;
    }

    public override void _ExitTree()
    {
        MouseEntered -= OnMouseEntered;
        MouseExited -= OnMouseExited;
        Pressed -= OnPressed;
    }

    private void OnMouseEntered()
    {
        if (Character is IGroupableCharacter { AllowSelect: false })
            return;

        ApplyVisualState(true);
    }

    private void OnMouseExited()
    {
        if (IsSelected)
            return;

        ApplyVisualState(false);
    }

    private void ApplyVisualState(bool active)
    {
        _hoverTween?.Kill();

        var targetPosition = _originalPosition + (active ? ActiveOffset : Vector2.Zero);

        _hoverTween = CreateTween();
        _hoverTween.SetParallel()
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        _hoverTween.TweenProperty(this, "position", targetPosition, AnimationDuration);
    }

    private void OnPressed()
    {
        if (Character is IGroupableCharacter { AllowSelect: false })
        {
            var randomTips = Rng.Chaotic.NextInt(0, 3);
            var locString = new LocString("gameplay_ui", $"{MessagePrefixKey}{randomTips}.message");
            RitsuToastService.ShowInfo(locString.GetFormattedText(), Character.Title.GetFormattedText());
            return;
        }

        if (!IsSelected && Character != null)
        {
            Select();
            _parent?.SelectCharacter(Character);
        }
    }

    public void Deselect()
    {
        IsSelected = false;
        OnMouseExited();
    }

    public void Select()
    {
        IsSelected = true;
        OnMouseEntered();
    }
}