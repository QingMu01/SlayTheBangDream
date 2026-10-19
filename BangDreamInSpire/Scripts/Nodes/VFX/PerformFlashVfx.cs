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

    /// <summary>场景基准槽位尺寸（与 SweepLight 的 160×50 一致）；实际槽位更宽时按此比值横向铺开。</summary>
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

    /// <summary>
    /// 槽位未缩放的显示尺寸（缺省 160×50，即场景基准）。特效据此把扫光、星芒与光尘按槽位铺开；
    /// 游戏内由演奏区注入，预览时可在 Inspector 里改大以核对宽槽位下的表现。
    /// </summary>
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

    /// <summary>
    /// 游戏内由演奏区注入未缩放尺寸与相对于演奏区的变换（含中心、缩放与镜像）。
    /// </summary>
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

    /// <summary>
    /// 按槽位大小铺开：扫光矩形与光尘发射盒随槽位拉宽，星芒/晶片的横向位置等比外扩，
    /// 亮起时刻按新几何重算。元素自身的笔触大小保持不变，避免宽槽位下被横向拉扁。
    /// </summary>
    private void ApplySlotFit()
    {
        // 编辑器里不写任何运行期状态，保留场景作者的摆放。
        if (Engine.IsEditorHint())
            return;

        _slotFit = new Vector2(_slotSize.X / DesignSlotSize.X, _slotSize.Y / DesignSlotSize.Y);
        if (!IsInsideTree() || !VfxPreviewSupport.IsPreview(this))
            Transform = _slotTransform;

        if (_sweepLight != null)
        {
            _sweepLight.Position = -_slotSize * 0.5f;
            _sweepLight.Size = _slotSize;
            // 扫光着色器以 rect_size 为几何基准（斜边、带宽、前沿行程都在其中换算），必须同步。
            _sweepLight.SetInstanceShaderParameter(RectSizeShaderParameter, _slotSize);
        }

        if (_glowDust != null)
        {
            _glowDust.VisibilityRect = ScaleRect(_dustBaseVisibilityRect, _slotFit);
            // 发射盒拉宽后单位面积粒子数会变稀，按同一比值补足数量以维持密度。
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

    /// <summary>
    /// 按扫光的 Sine/InOut 前沿反解每个亮点的亮起时刻。几何随槽位变化后必须重算，
    /// 否则宽槽位下两侧元素会与扫光前沿错位。
    /// </summary>
    private void RefreshAccentDelays()
    {
        _glints = [.. _glints.Select(accent => accent with { Delay = ResolveSweepDelay(accent.Position) })];
        _shards = [.. _shards.Select(accent => accent with { Delay = ResolveSweepDelay(accent.Position) })];
    }

    /// <summary>
    /// 反解某个亮点（场景基准坐标）被扫光前沿扫到的时刻：斜边、带宽与柔边从材质读取，
    /// 亮点位置按当前槽位铺开后的坐标参与换算。
    /// </summary>
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

    /// <summary>重置并重新播放一次特效。</summary>
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
        // 发射位置随槽位铺开；上浮距离使用场景基准，避免长卡名让晶片飞出 UI。
        var anchor = accent.Position * _slotFit;
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
