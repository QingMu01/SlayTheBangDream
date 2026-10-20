using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using STS2RitsuLib.Ui.Toast;

namespace BangDreamInSpire.Scripts.Nodes.SubNode;

/// <summary>
/// 角色选择页的成员卡：从角色立绘裁出脸部特写做头像，底部压一条主题色名字条。
/// </summary>
public partial class NCharacterButton : Button
{
    public static readonly Vector2 CardSize = new(126f, 180f);

    /// <summary>
    /// 立绘为 1800x2160 的全身图，人物脸部大致落在画布 33%~67% 宽、3%~37% 高 的范围内。
    /// </summary>
    private static readonly Rect2 FaceRegionRatio = new(0.33f, 0.03f, 0.34f, 0.34f);

    private const string MessagePrefixKey = "BANG_DREAM_IN_SPIRE_CHARACTER_CANT_SELECT_";

    private const float AnimationDuration = 0.16f;
    private const float ActiveScale = 1.04f;

    private static readonly Vector2 SelectedOffset = new(0f, -12f);
    private static readonly Vector2 HoverOffset = new(0f, -6f);

    private BangDreamCharacterSelector? _parent;

    private Panel? _frame;
    private TextureRect? _face;
    private Panel? _namePlate;
    private Label? _nameLabel;
    private TextureRect? _outline;

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

        _frame = GetNode<Panel>("Frame");
        _face = GetNode<TextureRect>("FaceClip/Face");
        _namePlate = GetNode<Panel>("NamePlate");
        _nameLabel = GetNode<Label>("NamePlate/Name");
        _outline = GetNode<TextureRect>("Outline");

        var accentColor = Colors.White;
        if (Character is { } character)
        {
            var bandColor = (character as IGroupableCharacter)?.Group.GetGroupInfo().Color ?? Colors.White;
            accentColor = bandColor.ToAccentColor();
            ApplyFace(character);
            _nameLabel.Text = new LocString("characters", character.CharacterSelectTitle).GetFormattedText();
        }

        ApplyAccent(accentColor);
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
    /// 用立绘的脸部区域做头像，省掉为每个角色单独维护一张头像图。
    /// </summary>
    private void ApplyFace(CharacterModel character)
    {
        if (_face == null
            || character is not IBangDreamMateData { SelectPoster: { } posterPath }
            || string.IsNullOrWhiteSpace(posterPath)
            || !ResourceLoader.Exists(posterPath))
        {
            return;
        }

        var texture = BangDreamPreloadManager.GetTexture2D(posterPath);
        var size = texture.GetSize();
        if (size.X <= 0f || size.Y <= 0f)
        {
            return;
        }

        _face.Texture = new AtlasTexture
        {
            Atlas = texture,
            Region = new Rect2(
                size.X * FaceRegionRatio.Position.X,
                size.Y * FaceRegionRatio.Position.Y,
                size.X * FaceRegionRatio.Size.X,
                size.Y * FaceRegionRatio.Size.Y)
        };
    }

    private void ApplyAccent(Color accentColor)
    {
        if (_frame?.GetThemeStylebox("panel") is StyleBoxFlat frameStyleBox)
        {
            var duplicated = (StyleBoxFlat)frameStyleBox.Duplicate();
            duplicated.BorderColor = accentColor;
            _frame.AddThemeStyleboxOverride("panel", duplicated);
        }

        if (_namePlate?.GetThemeStylebox("panel") is StyleBoxFlat namePlateStyleBox)
        {
            var duplicated = (StyleBoxFlat)namePlateStyleBox.Duplicate();
            duplicated.BgColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.92f);
            _namePlate.AddThemeStyleboxOverride("panel", duplicated);
        }

        if (_outline != null)
        {
            _outline.SelfModulate = accentColor;
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
        var liftedOffset = IsSelected ? SelectedOffset : active ? HoverOffset : Vector2.Zero;
        var targetPosition = _originalPosition + liftedOffset;
        var targetScale = IsSelected || active ? new Vector2(ActiveScale, ActiveScale) : Vector2.One;

        if (_outline != null)
        {
            _outline.Visible = IsSelected;
        }

        _hoverTween?.Kill();

        if (instant)
        {
            Position = targetPosition;
            Scale = targetScale;
            return;
        }

        _hoverTween = CreateTween();
        _hoverTween.SetParallel()
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        _hoverTween.TweenProperty(this, "position", targetPosition, AnimationDuration);
        _hoverTween.TweenProperty(this, "scale", targetScale, AnimationDuration);
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
