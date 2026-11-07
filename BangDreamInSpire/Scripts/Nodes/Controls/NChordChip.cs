using System;
using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Mechanics.Perform;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.ControllerInput;

namespace BangDreamInSpire.Scripts.Nodes.Controls;

/// <summary>
/// 和弦设计面板里的单和弦筹码（C / D / F / G）。
/// <para>
/// 自绘圆角方块：填充与描边取 <see cref="PerformChordColors" /> 的角色色，选中时描边更亮更粗；
/// 文本用 <see cref="MegaLabel" />（CJK 字体替换只对 mega_text 生效）。点击发 <see cref="Clicked" />。
/// </para>
/// </summary>
public partial class NChordChip : Control
{
    /// <summary>筹码边长（设计分辨率 1920×1080 下的像素）。</summary>
    public const float ChipSize = 88f;

    private const int CornerRadius = 16;
    private static readonly Font? LabelFont =
        ResourceLoader.Load<Font>("res://themes/fonts/jpn/noto_sans_cjkjp_medium_shared.tres");

    private readonly PerformChord _chord;
    private readonly Color _color;
    private readonly MegaLabel _label;

    private bool _selected;
    private bool _hovered;

    /// <summary>玩家点击该筹码时触发。</summary>
    public event Action<NChordChip>? Clicked;

    public NChordChip(PerformChord chord)
    {
        _chord = chord;
        _color = PerformChordColors.ColorOf(chord);
        Name = $"ChordChip-{chord}";
        Size = new Vector2(ChipSize, ChipSize);
        CustomMinimumSize = Size;
        PivotOffset = Size * 0.5f;
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;

        _label = new MegaLabel
        {
            Name = "Letter",
            Text = chord.ToString(),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutoSizeEnabled = false,
            MouseFilter = MouseFilterEnum.Ignore,
            // 铺满整个筹码，文字才会居中；Label 不设尺寸时只按文字最小尺寸排在左上角
            Position = Vector2.Zero,
            Size = new Vector2(ChipSize, ChipSize)
        };
        _label.AddThemeFontOverride("font", LabelFont ?? ThemeDB.FallbackFont);
        _label.AddThemeFontSizeOverride("font_size", 42);
        _label.AddThemeColorOverride("font_color", _color.Lightened(0.35f));
        _label.AddThemeConstantOverride("outline_size", 8);
        _label.AddThemeColorOverride("font_outline_color", new Color("#0d0e12"));
        AddChild(_label);

        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
        FocusEntered += OnMouseEntered;
        FocusExited += OnMouseExited;
    }

    /// <summary>该筹码代表的和弦。</summary>
    public PerformChord Chord => _chord;

    public bool IsSelected => _selected;

    public override void _Ready()
    {
        _label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        QueueRedraw();
    }

    /// <summary>设置选中态：选中后筹码会移动到「已选槽位」上方，描边随之加亮。</summary>
    public void SetSelected(bool selected)
    {
        if (_selected == selected)
        {
            return;
        }

        _selected = selected;
        QueueRedraw();
    }

    public void SetHovered(bool hovered)
    {
        if (_hovered == hovered)
        {
            return;
        }

        _hovered = hovered;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        var fillAlpha = _selected ? 0.42f : _hovered ? 0.30f : 0.18f;
        var borderAlpha = _selected ? 1.0f : _hovered ? 0.85f : 0.5f;
        var borderWidth = _selected ? 5 : 3;

        var background = new StyleBoxFlat
        {
            BgColor = new Color(_color.R, _color.G, _color.B, fillAlpha),
            BorderColor = new Color(_color.R, _color.G, _color.B, borderAlpha),
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = CornerRadius,
            CornerRadiusTopRight = CornerRadius,
            CornerRadiusBottomLeft = CornerRadius,
            CornerRadiusBottomRight = CornerRadius,
            AntiAliasing = true
        };
        DrawStyleBox(background, rect);

        if (_selected)
        {
            var glow = new StyleBoxFlat
            {
                BgColor = new Color(0, 0, 0, 0),
                BorderColor = new Color(_color.R, _color.G, _color.B, 0.28f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                CornerRadiusTopLeft = CornerRadius + 5,
                CornerRadiusTopRight = CornerRadius + 5,
                CornerRadiusBottomLeft = CornerRadius + 5,
                CornerRadiusBottomRight = CornerRadius + 5,
                ExpandMarginLeft = 5,
                ExpandMarginTop = 5,
                ExpandMarginRight = 5,
                ExpandMarginBottom = 5,
                AntiAliasing = true
            };
            DrawStyleBox(glow, rect);
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            Clicked?.Invoke(this);
            AcceptEvent();
            return;
        }

        // 只响应 select：confirm 是「确定」按钮的全局热键，筹码不能抢它
        if (@event.IsActionPressed(MegaInput.select))
        {
            Clicked?.Invoke(this);
            AcceptEvent();
        }
    }

    private void OnMouseEntered()
    {
        SetHovered(true);
        var tween = CreateTween();
        tween.TweenProperty(this, "scale", new Vector2(1.06f, 1.06f), 0.1f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    private void OnMouseExited()
    {
        SetHovered(false);
        var tween = CreateTween();
        tween.TweenProperty(this, "scale", Vector2.One, 0.1f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }
}
