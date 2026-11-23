using System;
using Godot;

namespace BangDreamInSpire.Scripts.Nodes.Controls;

/// <summary>横向滚动条，报告 0..1 位置并保持最小滑块宽度。</summary>
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

    /// <summary>位置变化时触发。</summary>
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

    /// <summary>更新滚动比例与可视比例。</summary>
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
