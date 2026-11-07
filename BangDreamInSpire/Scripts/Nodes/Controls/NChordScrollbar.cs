using System;
using Godot;

namespace BangDreamInSpire.Scripts.Nodes.Controls;

/// <summary>
/// 和弦设计面板卡牌行的横向滚动条（本体 <c>NScrollbar</c> 只有纵向，横向条是本面板自绘的）。
/// <para>
/// 只负责「外观 + 交互」：拖拽/点击时把位置换算成 <c>0..1</c> 的比例经 <see cref="Scrolled" /> 抛出，
/// 由面板决定如何滚动卡牌行；面板反过来用 <see cref="SetState" /> 把当前比例与可视比例写回来。
/// 两条限制：<see cref="MinThumbWidth" /> 保证内容再多也能抓住；比例恒在 <c>0..1</c> 内。
/// </para>
/// </summary>
public partial class NChordScrollbar : Control
{
    private const float TrackHeight = 10f;
    private const float MinThumbWidth = 64f;
    private static readonly Color TrackColor = new("#1b1d26");
    private static readonly Color ThumbColor = new("#8f9bb3");
    private static readonly Color ThumbHoverColor = new("#c7d2e8");

    private float _pageFraction = 1f;
    private float _ratio;
    private bool _dragging;
    private bool _hovered;

    /// <summary>用户把滑块拖到新位置时触发，参数为 <c>0..1</c> 的比例。</summary>
    public event Action<float>? Scrolled;

    public float Ratio => _ratio;

    public override void _Ready()
    {
        Visible = false; // 内容不足一屏时由 SetState 保持隐藏
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.None;
        MouseEntered += () => { _hovered = true; QueueRedraw(); };
        MouseExited += () => { _hovered = false; QueueRedraw(); };
    }

    /// <summary>写回状态：<paramref name="ratio" /> 为滚动比例，<paramref name="pageFraction" /> 为可视区占内容的比例。</summary>
    public void SetState(float ratio, float pageFraction)
    {
        var newPage = Mathf.Clamp(pageFraction, 0.04f, 1f);
        var newRatio = Mathf.Clamp(ratio, 0f, 1f);
        if (Mathf.IsEqualApprox(newPage, _pageFraction) && Mathf.IsEqualApprox(newRatio, _ratio))
        {
            return;
        }

        _pageFraction = newPage;
        _ratio = newRatio;
        Visible = _pageFraction < 0.999f;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var trackWidth = Size.X;
        if (trackWidth <= 0f)
        {
            return;
        }

        var trackY = (Size.Y - TrackHeight) * 0.5f;
        var track = new StyleBoxFlat
        {
            BgColor = TrackColor,
            CornerRadiusTopLeft = (int)(TrackHeight * 0.5f),
            CornerRadiusTopRight = (int)(TrackHeight * 0.5f),
            CornerRadiusBottomLeft = (int)(TrackHeight * 0.5f),
            CornerRadiusBottomRight = (int)(TrackHeight * 0.5f),
            AntiAliasing = true
        };
        DrawStyleBox(track, new Rect2(0f, trackY, trackWidth, TrackHeight));

        var thumbWidth = Mathf.Max(MinThumbWidth, trackWidth * _pageFraction);
        var thumbX = (trackWidth - thumbWidth) * _ratio;
        var thumb = new StyleBoxFlat
        {
            BgColor = _hovered || _dragging ? ThumbHoverColor : ThumbColor,
            CornerRadiusTopLeft = (int)(TrackHeight * 0.5f),
            CornerRadiusTopRight = (int)(TrackHeight * 0.5f),
            CornerRadiusBottomLeft = (int)(TrackHeight * 0.5f),
            CornerRadiusBottomRight = (int)(TrackHeight * 0.5f),
            AntiAliasing = true
        };
        DrawStyleBox(thumb, new Rect2(thumbX, trackY, thumbWidth, TrackHeight));
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                _dragging = button.Pressed;
                if (button.Pressed)
                {
                    EmitFromPosition(button.Position.X);
                    AcceptEvent();
                }

                QueueRedraw();
                break;
            case InputEventMouseMotion motion when _dragging:
                EmitFromPosition(motion.Position.X);
                AcceptEvent();
                break;
        }
    }

    private void EmitFromPosition(float localX)
    {
        var trackWidth = Size.X;
        if (trackWidth <= 0f)
        {
            return;
        }

        var thumbWidth = Mathf.Max(MinThumbWidth, trackWidth * _pageFraction);
        var travel = Mathf.Max(1f, trackWidth - thumbWidth);
        var ratio = Mathf.Clamp((localX - thumbWidth * 0.5f) / travel, 0f, 1f);
        if (Mathf.IsEqualApprox(ratio, _ratio))
        {
            return;
        }

        _ratio = ratio;
        QueueRedraw();
        Scrolled?.Invoke(ratio);
    }
}
