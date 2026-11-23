using System;
using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Mechanics.Perform;
using BangDreamInSpire.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.ControllerInput;

namespace BangDreamInSpire.Scripts.Nodes.Controls;

/// <summary>自绘和弦筹码，点击时发出 Clicked。</summary>
public partial class NChordChip : Control
{
    public const float ChipSize = 88f;

    private const int CornerRadius = 16;
    private static readonly Font? LabelFont =
        ResourceLoader.Load<Font>("res://themes/fonts/jpn/noto_sans_cjkjp_medium_shared.tres");

    private readonly PerformChord _chord;
    private readonly Color _color;
    private readonly MegaLabel _label;

    private bool _selected;
    private bool _hovered;

    /// <summary>点击筹码时触发。</summary>
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
            // Label 铺满筹码以居中。
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

    public PerformChord Chord => _chord;

    public bool IsSelected => _selected;

    public override void _Ready()
    {
        _label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        QueueRedraw();
    }

    /// <summary>设置选中态。</summary>
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

        // 不抢全局确定键。
        if (@event.IsActionPressed(MegaInput.select))
        {
            Clicked?.Invoke(this);
            AcceptEvent();
        }
    }

    private Tween? _scaleTween;

    private void OnMouseEntered()
    {
        SetHovered(true);
        _scaleTween?.Kill();
        _scaleTween = CreateTween();
        _scaleTween.TweenProperty(this, "scale", new Vector2(1.06f, 1.06f), 0.1f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    private void OnMouseExited()
    {
        SetHovered(false);
        _scaleTween?.Kill();
        _scaleTween = CreateTween();
        _scaleTween.TweenProperty(this, "scale", Vector2.One, 0.1f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }
}
