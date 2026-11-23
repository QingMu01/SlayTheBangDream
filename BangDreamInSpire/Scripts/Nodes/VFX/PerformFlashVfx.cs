using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamInSpire.Scripts.Nodes.VFX;

/// <summary>演奏扫光：由中心向两侧拨亮星芒，翻转的晶片与上浮光尘留下短暂余韵。</summary>
[Tool]
public partial class PerformFlashVfx : Node2D
{
    private static readonly StringName RevealProgressShaderParameter = "reveal_progress";
    private static readonly StringName RectSizeShaderParameter = "rect_size";

    /// <summary>场景基准槽位尺寸。</summary>
    private static readonly Vector2 DesignSlotSize = new(160f, 50f);

    private const float SweepDuration = 0.45f;
    private const float TotalDuration = 1f;
    private const float SweepAlpha = 0.7f;
    private const float GlintLifetime = 0.38f;
    private const float ShardLifetime = 0.52f;

    private ColorRect? _sweepLight;
    private bool _sweepBaseVisible;
    private GpuParticles2D? _glowDust;
    private Vector3 _dustBaseBoxExtents;
    private Rect2 _dustBaseVisibilityRect;
    private int _dustBaseAmount;
    private Accent[] _glints = [];
    private Accent[] _shards = [];
    private Tween? _tween;
    private Color _flashColor = new("#d30150");
    private Vector2 _slotSize = DesignSlotSize;
    private Transform2D _slotTransform = Transform2D.Identity;
    private Vector2 _slotFit = Vector2.One;

    // 所有运动按同一时间线求值，场景基准用于重播复位。
    private sealed record Accent(
        Sprite2D Sprite,
        Vector2 Position,
        Vector2 Scale,
        float Rotation,
        Color Modulate,
        float Delay);

    [Export]
    public Color FlashColor
    {
        get => _flashColor;
        set
        {
            _flashColor = value;
            ApplyFlashColor();
        }
    }

    /// <summary>槽位未缩放尺寸，驱动横向铺开。</summary>
    [Export]
    public Vector2 SlotSize
    {
        get => _slotSize;
        set
        {
            _slotSize = value is { X: > 0f, Y: > 0f } ? value : DesignSlotSize;
            ApplySlotFit();
            if (IsInsideTree() && VfxPreviewSupport.IsPreviewRun(this))
                Replay();
        }
    }

    [Export]
    public bool Play
    {
        get => false;
        set
        {
            if (value)
                Replay();
        }
    }

    [Export]
    public bool LoopPreview { get; set; } = true;

    public override void _Ready()
    {
        _sweepLight = GetNode<ColorRect>("SweepLight");
        _sweepBaseVisible = _sweepLight.Visible;
        _glowDust = GetNode<GpuParticles2D>("GlowDust");
        _dustBaseAmount = _glowDust.Amount;
        _dustBaseVisibilityRect = _glowDust.VisibilityRect;
        if (_glowDust.ProcessMaterial is ParticleProcessMaterial dust)
        {
            _dustBaseBoxExtents = dust.EmissionBoxExtents;
        }

        _glints = CaptureAccents(GetNode<Node2D>("Glints"));
        _shards = CaptureAccents(GetNode<Node2D>("Shards"));
        if (VfxPreviewSupport.AutoPlayOnReady)
            Replay();
    }

    /// <summary>注入槽位尺寸与相对变换。</summary>
    public void ConfigureSlot(Vector2 slotSize, Transform2D slotTransform)
    {
        _slotSize = slotSize is { X: > 0f, Y: > 0f } ? slotSize : DesignSlotSize;
        _slotTransform = slotTransform;
        ApplySlotFit();
    }

    private Accent[] CaptureAccents(Node2D group) =>
    [
        .. group.GetChildren().OfType<Sprite2D>()
            .Select(sprite => new Accent(sprite, sprite.Position, sprite.Scale, sprite.Rotation, sprite.Modulate, 0f))
    ];

    /// <summary>按槽位宽度调整几何，保持元素笔触尺寸。</summary>
    private void ApplySlotFit()
    {
        // 编辑器保留场景摆放。
        if (Engine.IsEditorHint())
            return;

        _slotFit = new Vector2(_slotSize.X / DesignSlotSize.X, _slotSize.Y / DesignSlotSize.Y);
        if (!IsInsideTree() || !VfxPreviewSupport.IsPreview(this))
            Transform = _slotTransform;

        if (_sweepLight != null)
        {
            _sweepLight.Position = -_slotSize * 0.5f;
            _sweepLight.Size = _slotSize;
            // 同步扫光着色器的 rect_size。
            _sweepLight.SetInstanceShaderParameter(RectSizeShaderParameter, _slotSize);
        }

        if (_glowDust != null)
        {
            _glowDust.VisibilityRect = ScaleRect(_dustBaseVisibilityRect, _slotFit);
            // 按宽度补足粒子数量以保持密度。
            var amount = Mathf.Max(1, Mathf.RoundToInt(_dustBaseAmount * _slotFit.X * _slotFit.Y));
            if (_glowDust.Amount != amount)
                _glowDust.Amount = amount;
            if (_glowDust.ProcessMaterial is ParticleProcessMaterial dust)
            {
                dust.EmissionBoxExtents = new Vector3(
                    _dustBaseBoxExtents.X * _slotFit.X,
                    _dustBaseBoxExtents.Y * _slotFit.Y,
                    _dustBaseBoxExtents.Z);
            }
        }

        RefreshAccentDelays();
    }

    private static Rect2 ScaleRect(Rect2 rect, Vector2 factor) => new(rect.Position * factor, rect.Size * factor);

    /// <summary>按扫光前沿计算亮点时刻。</summary>
    private void RefreshAccentDelays()
    {
        _glints = [.. _glints.Select(accent => accent with { Delay = ResolveSweepDelay(accent.Position) })];
        _shards = [.. _shards.Select(accent => accent with { Delay = ResolveSweepDelay(accent.Position) })];
    }

    /// <summary>反解单个亮点被扫到的时刻。</summary>
    private float ResolveSweepDelay(Vector2 designPosition)
    {
        if (_sweepLight == null)
            return 0f;

        var material = (ShaderMaterial)_sweepLight.Material;
        var slope = material.GetShaderParameter("slant_slope").AsSingle();
        var softness = material.GetShaderParameter("edge_softness").AsSingle();
        var size = _sweepLight.Size;
        var halfWidth = Mathf.Max((size.X - Mathf.Abs(slope * size.Y)) * 0.5f, 0.0001f);
        var band = Mathf.Min(material.GetShaderParameter("sweep_band_width").AsSingle() * 0.5f, halfWidth);
        var local = designPosition * _slotFit - _sweepLight.Position;
        var center = size.X * 0.5f + slope * (size.Y * 0.5f - local.Y);
        var distance = Mathf.Abs(local.X - center);
        var progress = Mathf.Clamp((distance - band * 0.5f) / (halfWidth + band + softness + 1f), 0f, 1f);
        return SweepDuration * Mathf.Acos(1f - 2f * progress) / Mathf.Pi;
    }

    public void Replay()
    {
        if (_sweepLight == null)
            return;

        _tween?.Kill();
        VfxPreviewSupport.CenterForPreview(this);
        ApplySlotFit();
        ApplyFlashColor();
        SetSweepProgress(0f);
        ApplyAccents(0f);
        _glowDust?.Restart();

        _tween = CreateTween();
        _tween.SetPauseMode(Tween.TweenPauseMode.Process);
        _tween.SetParallel();
        _tween.TweenMethod(Callable.From<float>(SetSweepProgress), 0f, 1f, SweepDuration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        _tween.TweenMethod(Callable.From<float>(ApplyAccents), 0f, TotalDuration, TotalDuration);
        _tween.Finished += OnPlaybackFinished;
    }

    private void ApplyAccents(float elapsed)
    {
        foreach (var accent in _glints)
            ApplyAccent(accent, elapsed, false);
        foreach (var accent in _shards)
            ApplyAccent(accent, elapsed, true);
    }

    private void ApplyAccent(Accent accent, float elapsed, bool isShard)
    {
        var lifetime = isShard ? ShardLifetime : GlintLifetime;
        var age = elapsed - accent.Delay;
        var progress = Mathf.Clamp(age / lifetime, 0f, 1f);
        var drift = 1f - Mathf.Pow(1f - progress, 2f);
        // 发射位置随槽位铺开，上浮距离保持基准。
        var anchor = accent.Position * _slotFit;
        var side = accent.Position.X < 0f ? -1f : 1f;
        var lift = isShard ? 24f + Mathf.Abs(accent.Position.X) * 0.22f : 10f;
        var offset = new Vector2(side * (isShard ? 14f : 7f), -lift) * drift;

        // 晶片翻面，星芒轻摆。
        var fadeIn = Mathf.Clamp(age / (isShard ? 0.055f : 0.035f), 0f, 1f);
        var fadeOut = 1f - Mathf.SmoothStep(0.25f, 1f, progress);
        var alpha = fadeIn * fadeOut;
        var scale = isShard
            ? new Vector2(Mathf.Lerp(0.2f, 1f, fadeIn) * Mathf.Max(0.12f, Mathf.Abs(Mathf.Cos(progress * Mathf.Tau))),
                Mathf.Lerp(0.6f, 1f, fadeIn) * (1f - progress * 0.35f))
            : new Vector2(Mathf.Lerp(0.35f, 1f, fadeIn) * (1f - progress * 0.55f),
                Mathf.Lerp(0.4f, 1.4f, fadeIn) * (1f - progress * 0.65f));

        accent.Sprite.Visible = age >= 0f && age < lifetime;
        accent.Sprite.Position = anchor + offset;
        accent.Sprite.Scale = accent.Scale * scale;
        accent.Sprite.Rotation = accent.Rotation + side * progress * (isShard ? 0.65f : 0.12f);
        var color = accent.Modulate;
        color.A *= alpha;
        accent.Sprite.Modulate = color;
    }

    private void OnPlaybackFinished()
    {
        if (VfxPreviewSupport.ShouldSelfFree(this))
        {
            this.QueueFreeSafely();
            return;
        }

        if (LoopPreview)
            Replay();
    }

    private void SetSweepProgress(float progress)
    {
        if (_sweepLight == null)
            return;

        _sweepLight.SetInstanceShaderParameter(RevealProgressShaderParameter, Mathf.Clamp(progress, 0f, 1f));
        // 前沿出界后停止绘制。
        _sweepLight.Visible = _sweepBaseVisible && progress < 1f;
    }

    private void ApplyFlashColor()
    {
        if (_sweepLight != null)
        {
            var sweepColor = _flashColor.Lightened(0.35f);
            sweepColor.A = SweepAlpha;
            _sweepLight.Color = sweepColor;
        }

        // 使用实例着色避免修改共享材质。
        var tint = new Color(_flashColor.R, _flashColor.G, _flashColor.B);
        foreach (var accent in _glints)
            accent.Sprite.SelfModulate = tint.Lightened(0.72f);
        foreach (var accent in _shards)
            accent.Sprite.SelfModulate = tint.Lightened(0.38f);
        if (_glowDust != null)
            _glowDust.SelfModulate = tint.Lightened(0.6f);
    }
}
