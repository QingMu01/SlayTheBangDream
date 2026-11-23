using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamInSpire.Scripts.Nodes.VFX;

/// <summary>
/// 演奏印框：槽位斜切边点亮 → 中心落印 → 元素墨迹按各自方向散尽。
/// 五个元素印共用这一条时间线，场景只提供 tints、节拍与墨迹
/// （<see cref="PerformSealStreak"/> / <see cref="PerformSealEmitter"/>），
/// 因此五张牌读起来是同一枚印的五种墨色，而不是五个通用光斑。
/// </summary>
[Tool]
public partial class PerformSealVfx : Node2D
{
    private static readonly StringName RectSizeParameter = "rect_size";
    private static readonly StringName DrawProgressParameter = "draw_progress";
    private static readonly StringName ReleaseProgressParameter = "release_progress";
    private static readonly StringName EnergyParameter = "energy";
    private static readonly StringName TintParameter = "tint";
    private static readonly StringName BandPositionParameter = "band_position";
    private static readonly StringName BandAlphaParameter = "band_alpha";
    private static readonly StringName BaseAlphaParameter = "base_alpha";

    private const string FlowNodeName = "Flow";
    private const string RimNodeName = "Rim";
    private const string WashNodeName = "Wash";
    private const string CoreNodeName = "Core";
    private const string StreaksNodeName = "Streaks";
    private const string EmittersNodeName = "Emitters";

    /// <summary>槽位基准尺寸；卡名更长时横向按比例铺开，短卡名不缩。</summary>
    private static readonly Vector2 DesignSlotSize = new(160f, 50f);

    // ── 共享时间线（五张牌一致，改这里等于改整套设计语言）──
    // 墨流：一条带拖尾的元素色墨带从左向右扫过槽位。加法层在浅色卡图（风/水）上读不出来，
    // 这一层是那两张牌的对比度来源；同时它把"演奏"读成一个有方向的经过动作。
    private const float FlowStart = 0.02f;
    private const float FlowTravelTime = 0.30f;
    private const float FlowBandAlpha = 0.55f;
    private const float FlowBaseAlpha = 0.13f;
    private const float FlowEndTime = 0.42f;
    /// <summary>墨带从画外左侧进、到画外右侧出。</summary>
    private const float FlowBandFrom = -0.35f;
    private const float FlowBandTo = 1.35f;

    private const float RimDrawTime = 0.16f;
    private const float RimReleaseStart = 0.26f;
    private const float RimReleaseTime = 0.22f;
    private const float RimEndTime = 0.50f;

    private const float WashRiseTime = 0.06f;
    private const float WashEndTime = 0.30f;
    private const float WashPeakAlpha = 0.22f;
    private const float WashScaleGain = 0.16f;

    private const float CoreRiseTime = 0.055f;
    private const float CorePeakTime = 0.10f;
    private const float CoreEndTime = 0.34f;
    private const float CorePeakAlpha = 0.95f;
    private const float CoreStartScale = 0.55f;
    private const float CorePulseGain = 0.42f;

    /// <summary>拍子脉冲的衰减时长。</summary>
    private const float PulseDecay = 0.12f;

    /// <summary>本体色：印框与墨迹的主色。</summary>
    [Export]
    public Color BodyTint { get; set; } = new("#ff8a34");

    /// <summary>高光色：中心落印的白热点。</summary>
    [Export]
    public Color CoreTint { get; set; } = new("#fff4d8");

    /// <summary>辅色：墨迹笔尖的过渡色。</summary>
    [Export]
    public Color AccentTint { get; set; } = new("#d8402a");

    /// <summary>深色墨：MIX 混合扫过槽位的底色，深浅卡图上都能读出元素归属。</summary>
    [Export]
    public Color DeepTint { get; set; } = new("#8a2e0e");

    /// <summary>节拍时刻（秒，升序）。每次脉冲会点亮落印并发射对应拍的墨迹。</summary>
    [Export]
    public float[] Beats { get; set; } = [0.10f];

    /// <summary>整段时长；设计上不超过 0.6 秒。</summary>
    [Export(PropertyHint.Range, "0.2,1.2,0.01")]
    public float LifeTime { get; set; } = 0.56f;

    /// <summary>播放结束自毁；合成预览场景里关掉，避免整块特效被回收。</summary>
    [Export]
    public bool AutoFree { get; set; } = true;

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

    private ColorRect? _flow;
    private ColorRect? _rim;
    private Sprite2D? _wash;
    private Sprite2D? _core;
    private PerformSealStreak[] _streaks = [];
    private PerformSealEmitter[] _emitters = [];

    // 从场景读回的美术基准值，重播与自适应时按此换算（不写死初值）。
    private Vector2 _washBaseScale;
    private Vector2 _coreBaseScale;
    private Vector2[] _streakBasePositions = [];
    private Vector2[] _streakBaseScales = [];
    private float[] _streakBaseRotations = [];
    private Vector2[] _emitterBasePositions = [];
    private Vector3[] _emitterBaseBoxExtents = [];

    private Vector2 _slotSize = DesignSlotSize;
    private Transform2D _slotTransform = Transform2D.Identity;
    private bool _hasSlotTransform;
    private Vector2 _slotFit = Vector2.One;
    private Tween? _tween;
    private int _nextBeat;
    private float _elapsed;

    public override void _Ready()
    {
        _flow = GetNodeOrNull<ColorRect>(FlowNodeName);
        _rim = GetNodeOrNull<ColorRect>(RimNodeName);
        _wash = GetNodeOrNull<Sprite2D>(WashNodeName);
        _core = GetNodeOrNull<Sprite2D>(CoreNodeName);
        if (_wash != null) _washBaseScale = _wash.Scale;
        if (_core != null) _coreBaseScale = _core.Scale;

        _streaks = CaptureSequence<PerformSealStreak>(StreaksNodeName);
        _streakBasePositions = [.. _streaks.Select(streak => streak.Position)];
        _streakBaseScales = [.. _streaks.Select(streak => streak.Scale)];
        _streakBaseRotations = [.. _streaks.Select(streak => streak.Rotation)];

        _emitters = CaptureSequence<PerformSealEmitter>(EmittersNodeName);
        _emitterBasePositions = [.. _emitters.Select(emitter => emitter.Position)];
        _emitterBaseBoxExtents = [.. _emitters.Select(ResolveBoxExtents)];

        if (VfxPreviewSupport.AutoPlayOnReady)
            Replay();
    }

    private T[] CaptureSequence<T>(string groupName) where T : Node
    {
        var group = GetNodeOrNull<Node2D>(groupName);
        return group == null ? [] : [.. group.GetChildren().OfType<T>()];
    }

    private static Vector3 ResolveBoxExtents(PerformSealEmitter emitter)
    {
        return emitter.ProcessMaterial is ParticleProcessMaterial material &&
               material.EmissionShape == ParticleProcessMaterial.EmissionShapeEnum.Box
            ? material.EmissionBoxExtents
            : Vector3.One;
    }

    /// <summary>注入槽位尺寸与相对变换；调用方在入树前设置。</summary>
    public void ConfigureSlot(Vector2 slotSize, Transform2D slotTransform)
    {
        _slotSize = slotSize is { X: > 0f, Y: > 0f } ? slotSize : DesignSlotSize;
        _slotTransform = slotTransform;
        _hasSlotTransform = true;
        ApplySlotFit();
    }

    /// <summary>铺开几何：印框贴合槽位，墨迹沿宽度散开，笔粗细保持不变。</summary>
    private void ApplySlotFit()
    {
        // 编辑器里保留作者摆放。
        if (Engine.IsEditorHint())
            return;

        _slotFit = new Vector2(
            Mathf.Max(_slotSize.X / DesignSlotSize.X, 0.01f),
            Mathf.Max(_slotSize.Y / DesignSlotSize.Y, 0.01f));

        // 未注入槽位变换时（合成预览）保留作者摆放。
        if (_hasSlotTransform && (!IsInsideTree() || !VfxPreviewSupport.IsPreview(this)))
            Transform = _slotTransform;

        foreach (var rect in new[] { _flow, _rim })
        {
            if (rect == null) continue;
            rect.Position = -_slotSize * 0.5f;
            rect.Size = _slotSize;
            rect.SetInstanceShaderParameter(RectSizeParameter, _slotSize);
        }

        if (_wash != null)
        {
            _wash.Scale = new Vector2(_washBaseScale.X * _slotFit.X, _washBaseScale.Y * _slotFit.Y);
        }

        for (var index = 0; index < _streaks.Length; index++)
        {
            _streaks[index].Position = _streakBasePositions[index] * _slotFit;
        }

        for (var index = 0; index < _emitters.Length; index++)
        {
            var emitter = _emitters[index];
            emitter.Position = _emitterBasePositions[index] * _slotFit;
            // 粒子材质是 local_to_scene，改它不会污染其他实例；编辑器已提前返回。
            if (emitter.ProcessMaterial is not ParticleProcessMaterial material) continue;
            if (material.EmissionShape != ParticleProcessMaterial.EmissionShapeEnum.Box) continue;
            var extents = _emitterBaseBoxExtents[index];
            material.EmissionBoxExtents = new Vector3(
                Mathf.Max(extents.X * _slotFit.X, 1f),
                Mathf.Max(extents.Y * _slotFit.Y, 1f),
                extents.Z);
        }
    }

    public void Replay()
    {
        _tween?.Kill();
        _tween = null;
        VfxPreviewSupport.CenterForPreview(this);
        ApplySlotFit();
        ApplyTints();
        ResetLayers();

        _tween = CreateTween();
        _tween.SetPauseMode(Tween.TweenPauseMode.Process);
        _tween.TweenMethod(Callable.From<float>(ApplyTimeline), 0f, LifeTime, LifeTime);
        _tween.Finished += OnPlaybackFinished;
    }

    /// <summary>把元素配色落到粒团上；材质本身保持中性，靠节点着色避免污染共享材质。</summary>
    private void ApplyTints()
    {
        foreach (var emitter in _emitters)
        {
            emitter.SelfModulate = emitter.HasOwnTint ? emitter.Tint : AccentTint;
        }
    }

    /// <summary>复位到"什么都不显示"，避免重播时残留上一轮的状态。</summary>
    private void ResetLayers()    {
        _nextBeat = 0;
        ApplyRimState(0f, 0f);
        if (_flow != null) _flow.Visible = false;
        if (_rim != null) _rim.Visible = false;
        if (_wash != null) _wash.Visible = false;
        if (_core != null) _core.Visible = false;
        foreach (var streak in _streaks) streak.Visible = false;
        foreach (var emitter in _emitters) emitter.Emitting = false;
    }

    /// <summary>单一时间线：所有层由 elapsed 求值，避免并行 tween 互相覆盖。</summary>
    private void ApplyTimeline(float elapsed)
    {
        _elapsed = elapsed;
        ApplyFlow(elapsed);
        ApplyRimState(
            Mathf.Clamp(elapsed / RimDrawTime, 0f, 1f),
            Mathf.Clamp((elapsed - RimReleaseStart) / RimReleaseTime, 0f, 1f));
        if (_rim != null) _rim.Visible = elapsed < RimEndTime;
        ApplyWash(elapsed);
        ApplyCore(elapsed);
        ApplyStreaks(elapsed);
        ApplyBeats(elapsed);
    }

    /// <summary>墨流：墨带扫过槽位，带身最重、全程留一层薄底；0.42s 前退干净。</summary>
    private void ApplyFlow(float elapsed)
    {
        if (_flow == null) return;
        if (elapsed >= FlowEndTime)
        {
            _flow.Visible = false;
            return;
        }

        var travel = Mathf.Clamp((elapsed - FlowStart) / FlowTravelTime, 0f, 1f);
        var rise = Mathf.Clamp((elapsed - FlowStart) / 0.05f, 0f, 1f);
        var fade = elapsed <= FlowStart + FlowTravelTime
            ? 1f
            : 1f - Mathf.SmoothStep(0f, 1f, (elapsed - FlowStart - FlowTravelTime) /
                                        (FlowEndTime - FlowStart - FlowTravelTime));
        var bandAlpha = FlowBandAlpha * rise * fade;
        var baseAlpha = FlowBaseAlpha * rise * fade;
        _flow.Visible = bandAlpha > 0.002f || baseAlpha > 0.002f;
        _flow.Color = WithAlpha(DeepTint, 1f);
        _flow.SetInstanceShaderParameter(BandPositionParameter,
            Mathf.Lerp(FlowBandFrom, FlowBandTo, EaseInOut(travel)));
        _flow.SetInstanceShaderParameter(BandAlphaParameter, bandAlpha);
        _flow.SetInstanceShaderParameter(BaseAlphaParameter, baseAlpha);
    }

    private static float EaseInOut(float value)
    {
        var clamped = Mathf.Clamp(value, 0f, 1f);
        return clamped * clamped * (3f - 2f * clamped);
    }

    private void ApplyRimState(float draw, float release)
    {
        if (_rim == null) return;
        var tint = BodyTint;
        tint.A = 0.85f;
        _rim.SetInstanceShaderParameter(DrawProgressParameter, draw);
        _rim.SetInstanceShaderParameter(ReleaseProgressParameter, release);
        _rim.SetInstanceShaderParameter(EnergyParameter, 1f + 0.9f * ResolvePulse(_elapsed));
        _rim.SetInstanceShaderParameter(TintParameter, tint);
    }

    private void ApplyWash(float elapsed)
    {
        if (_wash == null) return;
        if (elapsed >= WashEndTime)
        {
            _wash.Visible = false;
            return;
        }

        var rise = Mathf.Clamp(elapsed / WashRiseTime, 0f, 1f);
        var fall = 1f - Mathf.Pow(Mathf.Clamp(elapsed / WashEndTime, 0f, 1f), 1.6f);
        var alpha = WashPeakAlpha * rise * fall;
        var grow = 1f + WashScaleGain * EaseOut(elapsed / WashEndTime);
        _wash.Visible = alpha > 0.002f;
        _wash.SelfModulate = WithAlpha(BodyTint, alpha);
        _wash.Scale = new Vector2(
            _washBaseScale.X * _slotFit.X * grow,
            _washBaseScale.Y * _slotFit.Y * grow);
    }

    private void ApplyCore(float elapsed)
    {
        if (_core == null) return;
        if (elapsed >= CoreEndTime)
        {
            _core.Visible = false;
            return;
        }

        var rise = Mathf.Clamp(elapsed / CoreRiseTime, 0f, 1f);
        var fade = elapsed <= CorePeakTime
            ? 1f
            : 1f - Mathf.SmoothStep(0f, 1f, (elapsed - CorePeakTime) / (CoreEndTime - CorePeakTime));
        var pulse = ResolvePulse(elapsed);
        var grow = Mathf.Lerp(CoreStartScale, 1f, EaseOut(rise)) * (1f + CorePulseGain * pulse);
        _core.Visible = fade > 0.002f;
        var peak = Mathf.Min(CorePeakAlpha * rise * fade * (1f + 0.35f * pulse), 1f);
        _core.SelfModulate = WithAlpha(CoreTint, peak);
        _core.Scale = _coreBaseScale * grow;
    }

    private void ApplyStreaks(float elapsed)
    {
        for (var index = 0; index < _streaks.Length; index++)
        {
            var streak = _streaks[index];
            var age = elapsed - streak.StartTime;
            if (age < 0f || age >= streak.LifeTime)
            {
                streak.Visible = false;
                continue;
            }

            var progress = age / streak.LifeTime;
            var rise = Mathf.Clamp(age / Mathf.Max(streak.RiseTime, 0.001f), 0f, 1f);
            var alpha = streak.PeakAlpha * rise * (1f - Mathf.SmoothStep(0.35f, 1f, progress));
            var tint = streak.HasOwnTint ? streak.Tint : AccentTint;

            streak.Visible = alpha > 0.002f;
            streak.Position = _streakBasePositions[index] * _slotFit +
                              new Vector2(streak.EndOffset.X * _slotFit.X, streak.EndOffset.Y * _slotFit.Y) *
                              EaseOut(progress);
            streak.Scale = _streakBaseScales[index] * Mathf.Lerp(1f, streak.EndScaleMultiplier, progress);
            streak.Rotation = _streakBaseRotations[index] + Mathf.DegToRad(streak.SpinDegrees * progress);
            streak.SelfModulate = WithAlpha(tint, alpha);
        }
    }

    /// <summary>按 elapsed 补发所有已到点的拍子，慢帧时不会漏拍。</summary>
    private void ApplyBeats(float elapsed)
    {
        while (_nextBeat < Beats.Length && elapsed >= Beats[_nextBeat])
        {
            FireBeat(_nextBeat);
            _nextBeat++;
        }
    }

    private void FireBeat(int beatIndex)
    {
        foreach (var emitter in _emitters)
        {
            var emitterBeat = Math.Clamp(emitter.BeatIndex, 0, Beats.Length - 1);
            if (emitterBeat != beatIndex) continue;
            emitter.Emitting = true;
            emitter.Restart();
        }
    }

    /// <summary>拍子上叠加的衰减脉冲，用来给落印"打分"。</summary>
    private float ResolvePulse(float elapsed)
    {
        var pulse = 0f;
        foreach (var beat in Beats)
        {
            if (elapsed < beat) continue;
            pulse = Mathf.Max(pulse, Mathf.Max(0f, 1f - (elapsed - beat) / PulseDecay));
        }

        return pulse;
    }

    private static float EaseOut(float value)
    {
        var clamped = Mathf.Clamp(value, 0f, 1f);
        return 1f - Mathf.Pow(1f - clamped, 3f);
    }

    private static Color WithAlpha(Color color, float alpha) => new(color.R, color.G, color.B, alpha);

    private void OnPlaybackFinished()
    {
        if (AutoFree && VfxPreviewSupport.ShouldSelfFree(this))
        {
            this.QueueFreeSafely();
            return;
        }

        if (LoopPreview)
            Replay();
    }
}
