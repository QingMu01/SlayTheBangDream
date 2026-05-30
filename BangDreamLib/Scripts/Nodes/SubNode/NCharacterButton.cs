using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Nodes.SubNode;

public partial class NCharacterButton : Button
{
    private BangDreamCharacterSelector? _parent;

    private Tween? _hoverTween;

    private Vector2 _originalPosition;
    private bool _isSelected;

    public CharacterModel Character { get; private set; }

    public bool IsSelected
    {
        get => _isSelected;
        private set
        {
            if (Character is IAggregationCharacter aggregationCharacter)
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
        Modulate = Character.NameColor;
        _originalPosition = Position;

        if (IsSelected)
        {
            Position = _originalPosition + new Vector2(50f, 0f);
        }
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
        if (Character is IAggregationCharacter { AllowSelect: false })
            return;
        _hoverTween?.Kill();
        _hoverTween = CreateTween();
        _hoverTween.TweenProperty(this, "position", _originalPosition + new Vector2(50f, 0f), 0.1f);
    }

    private void OnMouseExited()
    {
        if (IsSelected)
            return;

        _hoverTween?.Kill();
        _hoverTween = CreateTween();
        _hoverTween.TweenProperty(this, "position", _originalPosition, 0.1f);
    }

    private void OnPressed()
    {
        if (Disabled || IsSelected || Character is IAggregationCharacter { AllowSelect: false })
            return;
        Select();

        _parent?.SelectCharacter(this);
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