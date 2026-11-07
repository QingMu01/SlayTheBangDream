using System;
using Godot;
using MegaCrit.Sts2.addons.mega_text;

namespace BangDreamInSpire.Scripts.Nodes.Controls;

/// <summary>
/// 和弦设计面板结果栏右侧的「增加 / 覆盖」开关（默认覆盖）。
/// <para>
/// 面板在未解锁时直接不给这个节点加进树（或用 <see cref="Visible" /> 隐藏），解锁逻辑由面板侧开关控制；
/// 本控件只负责自绘胶囊 + 滑块 + 两个文字标签，点击后切换并抛 <see cref="Toggled" />。
/// </para>
/// </summary>
public partial class NChordModeToggle : Control
{
    /// <summary>控件建议尺寸。</summary>
    public static readonly Vector2 ToggleSize = new(320f, 56f);

    private const float KnobInset = 6f;
    private static readonly Color PillColor = new("#1b1d26");
    private static readonly Color KnobColor = new("#7799CC");
    private static readonly Color ActiveTextColor = new("#e8eefc");
    private static readonly Color InactiveTextColor = new("#6f7789");
    private static readonly Font? LabelFont =
        ResourceLoader.Load<Font>("res://themes/fonts/jpn/noto_sans_cjkjp_medium_shared.tres");

    private readonly MegaLabel _addLabel;
    private readonly MegaLabel _overwriteLabel;
    private bool _additive;

    /// <summary>开关状态变化时触发；参数 true 表示「增加」，false 表示「覆盖」。</summary>
    public event Action<bool>? Toggled;

    public NChordModeToggle(string addText, string overwriteText)
    {
        Name = "ChordModeToggle";
        Size = ToggleSize;
        CustomMinimumSize = ToggleSize;
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.None;

        _addLabel = CreateLabel(addText, "AddLabel", HorizontalAlignment.Left);
        _overwriteLabel = CreateLabel(overwriteText, "OverwriteLabel", HorizontalAlignment.Right);
        AddChild(_addLabel);
        AddChild(_overwriteLabel);
        RefreshTextColors();
    }

    /// <summary>true = 增加（与卡牌现有和弦取并集），false = 覆盖（直接替换）。</summary>
    public bool IsAdditive => _additive;

    public override void _Ready()
    {
        QueueRedraw();
    }

    public void SetAdditive(bool additive)
    {
        if (_additive == additive)
        {
            return;
        }

        _additive = additive;
        RefreshTextColors();
        QueueRedraw();
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        var pill = new StyleBoxFlat
        {
            BgColor = PillColor,
            BorderColor = new Color("#2a2d3a"),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 28,
            CornerRadiusTopRight = 28,
            CornerRadiusBottomLeft = 28,
            CornerRadiusBottomRight = 28,
            AntiAliasing = true
        };
        DrawStyleBox(pill, rect);

        var center = KnobCenter();
        DrawCircle(center, 22f, KnobColor);
        DrawCircle(center, 15f, new Color("#11131a"));
        DrawCircle(center, 10f, KnobColor.Lightened(0.15f));
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            SetAdditive(!_additive);
            Toggled?.Invoke(_additive);
            AcceptEvent();
        }
    }

    private Vector2 KnobCenter()
    {
        var x = _additive ? KnobInset + 22f : Size.X - KnobInset - 22f;
        return new Vector2(x, Size.Y * 0.5f);
    }

    private void RefreshTextColors()
    {
        _addLabel.AddThemeColorOverride("font_color", _additive ? ActiveTextColor : InactiveTextColor);
        _overwriteLabel.AddThemeColorOverride("font_color", _additive ? InactiveTextColor : ActiveTextColor);
    }

    private static MegaLabel CreateLabel(string text, string name, HorizontalAlignment alignment)
    {
        var label = new MegaLabel
        {
            Name = name,
            Text = text,
            AutoSizeEnabled = false,
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            // 两个标签分别贴胶囊左右两端，滑块在两端时都不会压到文字
            Position = new Vector2(alignment == HorizontalAlignment.Left ? 56f : ToggleSize.X - 56f - 90f, 0f),
            Size = new Vector2(90f, ToggleSize.Y)
        };
        label.AddThemeFontOverride("font", LabelFont ?? ThemeDB.FallbackFont);
        label.AddThemeFontSizeOverride("font_size", 22);
        return label;
    }
}
