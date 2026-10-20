using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Nodes.MegeScript;
using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using STS2RitsuLib.Ui.Toast;

namespace BangDreamInSpire.Scripts.Nodes.SubNode;

/// <summary>
/// 角色选择页压在卡片下缘的「书签」成员标签：成员色底 + 角色代表物纹章 + 名字 / 职业，
/// 选中时整体向上抽出并补一道白色描边。
/// </summary>
public partial class NCharacterButton : Button
{
    public static readonly Vector2 CardSize = new(84f, 104f);

    private const string MessagePrefixKey = "BANG_DREAM_IN_SPIRE_CHARACTER_CANT_SELECT_";

    private const float AnimationDuration = 0.16f;

    private static readonly Vector2 SelectedOffset = new(0f, -10f);
    private static readonly Vector2 HoverOffset = new(0f, -4f);

    private static readonly Color SelectedBorderColor = new(1f, 1f, 1f, 0.92f);
    private static readonly Color FallbackMemberColor = new(0.467f, 0.6f, 0.8f);

    private BangDreamCharacterSelector? _parent;

    private Panel? _tab;
    private TextureRect? _icon;
    private BangDreamLabel? _nameLabel;
    private BangDreamLabel? _classLabel;

    private StyleBoxFlat? _tabStyle;
    private StyleBoxFlat? _selectedTabStyle;

    private Tween? _hoverTween;

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
        _originalPosition = Position;

        _tab = GetNode<Panel>("Tab");
        _icon = GetNode<TextureRect>("%Icon");
        _nameLabel = GetNode<BangDreamLabel>("%Name");
        _classLabel = GetNode<BangDreamLabel>("%Class");

        var memberColor = FallbackMemberColor;
        if (Character is { } character)
        {
            memberColor = character.NameColor;
            ApplyIcon(character);
            _nameLabel.SetTextAutoSize(
                new LocString("characters", character.CharacterSelectTitle).GetFormattedText());
            if (character is IBangDreamMateData mateData)
            {
                _classLabel.SetTextAutoSize(mateData.MemberClass);
            }
        }

        BuildTabStyles(memberColor);
        ApplyVisualState(false, true);
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

    /// <summary>
    /// 用角色代表物（SelectLogo 纹章）做标签图标，替换原来的立绘裁脸。
    /// </summary>
    private void ApplyIcon(CharacterModel character)
    {
        if (_icon == null || character is not IBangDreamMateData { SelectLogo: { } logoPath })
        {
            return;
        }

        var texture = BangDreamCharacterSelector.LoadTexture(logoPath);
        if (texture == null)
        {
            return;
        }

        _icon.Texture = texture;
    }

    private void BuildTabStyles(Color memberColor)
    {
        var background = memberColor.Darkened(0.1f);
        var textColor = background.GetReadableTextColor();

        _tabStyle = new StyleBoxFlat
        {
            BgColor = background,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
            ShadowColor = new Color(0.05f, 0.08f, 0.12f, 0.18f),
            ShadowSize = 8,
            ShadowOffset = new Vector2(0f, 3f),
            AntiAliasing = true
        };

        _selectedTabStyle = (StyleBoxFlat)_tabStyle.Duplicate();
        _selectedTabStyle.SetBorderWidthAll(2);
        _selectedTabStyle.BorderColor = SelectedBorderColor;
        _selectedTabStyle.ShadowColor = new Color(0.03f, 0.05f, 0.08f, 0.3f);
        _selectedTabStyle.ShadowSize = 14;

        if (_tab != null)
        {
            _tab.AddThemeStyleboxOverride("panel", _tabStyle);
        }

        if (_nameLabel != null)
        {
            _nameLabel.AddThemeColorOverride("font_color", textColor);
        }

        if (_classLabel != null)
        {
            _classLabel.AddThemeColorOverride("font_color",
                new Color(textColor.R, textColor.G, textColor.B, 0.8f));
        }

        if (_icon != null)
        {
            _icon.Modulate = textColor;
        }
    }

    private void OnMouseEntered()
    {
        if (Character is IGroupableCharacter { AllowSelect: false })
            return;

        ApplyVisualState(true);
    }

    private void OnMouseExited()
    {
        ApplyVisualState(false);
    }

    private void ApplyVisualState(bool active, bool instant = false)
    {
        var targetPosition = _originalPosition +
                             (IsSelected ? SelectedOffset : active ? HoverOffset : Vector2.Zero);

        if (_tab != null)
        {
            _tab.AddThemeStyleboxOverride("panel",
                IsSelected && _selectedTabStyle != null ? _selectedTabStyle : _tabStyle);
        }

        _hoverTween?.Kill();

        if (instant)
        {
            Position = targetPosition;
            return;
        }

        _hoverTween = CreateTween();
        _hoverTween.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
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
        ApplyVisualState(false);
    }

    public void Select()
    {
        IsSelected = true;
        ApplyVisualState(false);
    }
}
