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
    private const float OutlineDuration = 0.22f;

    private static readonly Vector2 SelectedOffset = new(0f, -10f);
    private static readonly Vector2 HoverOffset = new(0f, -4f);

    private static readonly Color SelectedBorderColor = new(1f, 1f, 1f, 0.92f);
    private static readonly Color FallbackMemberColor = new(0.467f, 0.6f, 0.8f);

    private BangDreamCharacterSelector? _parent;

    private Panel? _tab;
    private TextureRect? _icon;
    private BangDreamLabel? _nameLabel;
    private BangDreamLabel? _classLabel;

    private StyleBoxFlat? _liveTabStyle;

    private Tween? _hoverTween;
    private Tween? _outlineTween;
    private float _outlineProgress;

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
        // 见 BangDreamTools.ConfigureMegaText：Mega 导出属性写在 tscn 里会被编辑器保存时置空。
        BangDreamTools.ConfigureMegaText(GetNodeOrNull<Control>("%Name"), maxFontSize: 12, minFontSize: 9);
        BangDreamTools.ConfigureMegaText(GetNodeOrNull<Control>("%Class"), maxFontSize: 9, minFontSize: 7);
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

        var baseStyle = new StyleBoxFlat
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

        // 面板始终用同一个 StyleBox 实例，选中/取消选中时动画它的描边与投影，
        // 这样切换角色能看到描边「长出来」的过程，而不是瞬间切换样式。
        _liveTabStyle = (StyleBoxFlat)baseStyle.Duplicate();
        ApplyOutline(_outlineProgress);

        if (_tab != null)
        {
            _tab.AddThemeStyleboxOverride("panel", _liveTabStyle);
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

        AnimateOutline(IsSelected, instant);
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

    /// <summary>
    /// 描边动画：0 = 无描边，1 = 完整白描边 + 加大的投影。选中时描边从 0 长出来（带一点回弹）。
    /// </summary>
    private void AnimateOutline(bool selected, bool instant)
    {
        if (_liveTabStyle == null)
        {
            return;
        }

        var target = selected ? 1f : 0f;
        _outlineTween?.Kill();

        if (instant)
        {
            ApplyOutline(_outlineProgress = target);
            return;
        }

        _outlineTween = CreateTween();
        _outlineTween.SetTrans(selected ? Tween.TransitionType.Back : Tween.TransitionType.Cubic);
        _outlineTween.SetEase(Tween.EaseType.Out);
        _outlineTween.TweenMethod(
            Callable.From<float>(value => ApplyOutline(_outlineProgress = value)),
            _outlineProgress, target, OutlineDuration);
    }

    private void ApplyOutline(float progress)
    {
        if (_liveTabStyle == null)
        {
            return;
        }

        var t = Mathf.Clamp(progress, 0f, 1.25f);
        _liveTabStyle.SetBorderWidthAll(Mathf.RoundToInt(2.4f * t));
        _liveTabStyle.BorderColor = new Color(
            SelectedBorderColor.R, SelectedBorderColor.G, SelectedBorderColor.B, SelectedBorderColor.A * t);
        _liveTabStyle.ShadowColor = new Color(0.05f, 0.08f, 0.12f, Mathf.Lerp(0.18f, 0.3f, t));
        _liveTabStyle.ShadowSize = Mathf.RoundToInt(Mathf.Lerp(8f, 14f, t));
        _liveTabStyle.ShadowOffset = new Vector2(0f, Mathf.Lerp(3f, 4.5f, t));
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
