using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamLib.Scripts.Nodes.VFX;

/// <summary>演奏扫光：由中心向两侧拨亮星芒，翻转的晶片与上浮光尘留下短暂余韵。</summary>
[Tool]
public partial class PerformFlashVfx : Node2D
{
    private static readonly StringName RevealProgressShaderParameter = "reveal_progress";

    private const float SweepDuration = 0.45f;
    private const float TotalDuration = 1f;
    private const float SweepAlpha = 0.7f;
    private const float GlintLifetime = 0.38f;
    private const float ShardLifetime = 0.52f;

    private ColorRect? _sweepLight;
    private bool _sweepBaseVisible;
    private GpuParticles2D? _glowDust;
    private Accent[] _glints = [];
    private Accent[] _shards = [];
    private Tween? _tween;
    private Color _flashColor = new("#d30150");

    // 基准值来自场景；所有运动按同一时间线求值，重播不会积累位移或缩放。
    private sealed record Accent(
        Sprite2D Sprite,
        Vector2 Position,
        Vector2 Scale,
        float Rotation,
        Color Modulate,
        float Delay);

    /// <summary>闪光主题色。游戏内由调用方按卡槽颜色注入，预览时可直接在 Inspector 里调。</summary>
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

    /// <summary>勾选即重播一次（自动复位，便于反复触发）。</summary>
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

    /// <summary>预览态下播放结束后自动重播。</summary>
    [Export]
    public bool LoopPreview { get; set; } = true;

    public override void _Ready()
    {
        _sweepLight = GetNode<ColorRect>("SweepLight");
        _sweepBaseVisible = _sweepLight.Visible;
        _glowDust = GetNode<GpuParticles2D>("GlowDust");
        _glints = CaptureAccents(GetNode<Node2D>("Glints"));
        _shards = CaptureAccents(GetNode<Node2D>("Shards"));

        if (VfxPreviewSupport.AutoPlayOnReady)
            Replay();
    }

    private Accent[] CaptureAccents(Node2D group) =>
    [
        .. group.GetChildren().OfType<Sprite2D>()
            .Select(sprite =>
            {
                // 按扫光的 Sine/InOut 前沿反解亮起时刻；倾斜率与带宽从场景读取。
                var material = (ShaderMaterial)_sweepLight!.Material;
                var slope = material.GetShaderParameter("slant_slope").AsSingle();
                var band = material.GetShaderParameter("sweep_band_width").AsSingle() * 0.5f;
                var softness = material.GetShaderParameter("edge_softness").AsSingle();
                var halfWidth = (_sweepLight.Size.X - Mathf.Abs(slope * _sweepLight.Size.Y)) * 0.5f;
                var local = sprite.Position - _sweepLight.Position;
                var center = _sweepLight.Size.X * 0.5f + slope * (_sweepLight.Size.Y * 0.5f - local.Y);
                var distance = Mathf.Abs(local.X - center);
                var progress = Mathf.Clamp((distance - band * 0.5f) / (halfWidth + band + softness + 1f), 0f, 1f);
                var delay = SweepDuration * Mathf.Acos(1f - 2f * progress) / Mathf.Pi;
                return new Accent(sprite, sprite.Position, sprite.Scale, sprite.Rotation, sprite.Modulate, delay);
            })
    ];

    /// <summary>重置并重新播放一次特效。</summary>
    public void Replay()
    {
        if (_sweepLight == null)
            return;

        _tween?.Kill();
        VfxPreviewSupport.CenterForPreview(this);
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

    private static void ApplyAccent(Accent accent, float elapsed, bool isShard)
    {
        var lifetime = isShard ? ShardLifetime : GlintLifetime;
        var age = elapsed - accent.Delay;
        var progress = Mathf.Clamp(age / lifetime, 0f, 1f);
        var drift = 1f - Mathf.Pow(1f - progress, 2f);
        var side = accent.Position.X < 0f ? -1f : 1f;
        var lift = isShard ? 24f + Mathf.Abs(accent.Position.X) * 0.22f : 10f;
        var offset = new Vector2(side * (isShard ? 14f : 7f), -lift) * drift;

        // 短促弹亮后再淡出。晶片绕纵轴翻面，星芒只轻摆。
        var fadeIn = Mathf.Clamp(age / (isShard ? 0.055f : 0.035f), 0f, 1f);
        var fadeOut = 1f - Mathf.SmoothStep(0.25f, 1f, progress);
        var alpha = fadeIn * fadeOut;
        var scale = isShard
            ? new Vector2(Mathf.Lerp(0.2f, 1f, fadeIn) * Mathf.Max(0.12f, Mathf.Abs(Mathf.Cos(progress * Mathf.Tau))),
                Mathf.Lerp(0.6f, 1f, fadeIn) * (1f - progress * 0.35f))
            : new Vector2(Mathf.Lerp(0.35f, 1f, fadeIn) * (1f - progress * 0.55f),
                Mathf.Lerp(0.4f, 1.4f, fadeIn) * (1f - progress * 0.65f));

        accent.Sprite.Visible = age >= 0f && age < lifetime;
        accent.Sprite.Position = accent.Position + offset;
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
        // 前沿出界后关闭绘制，避免斜边留下极细的抗锯齿残光。
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

        // 用实例着色，运行时和编辑器都不会修改场景共享的材质。
        var tint = new Color(_flashColor.R, _flashColor.G, _flashColor.B);
        foreach (var accent in _glints)
            accent.Sprite.SelfModulate = tint.Lightened(0.72f);
        foreach (var accent in _shards)
            accent.Sprite.SelfModulate = tint.Lightened(0.38f);
        if (_glowDust != null)
            _glowDust.SelfModulate = tint.Lightened(0.6f);
    }
}