using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace BangDreamInSpire.Scripts.Nodes.VFX;

/// <summary>
/// 演奏身印：同一枚元素之印落在演奏者身上——一道元素墨自脚底升过身体、从头顶之上淡出，
/// 胸口按下一枚与槽位同尺寸的小印并压出白热点，再由
/// <see cref="PerformSealStreak" /> / <see cref="PerformSealEmitter" /> 按元素方向散尽。
///
/// 与槽位印框（<see cref="PerformSealVfx" />）是同一枚印的两种落点：同一对 shader（墨走纵向档、
/// 小印走与槽位相同的横向档）、同一套配色、同一条时间线骨架；元素差异仍只落在
/// 运动方向、颗粒形态与节拍数。身上**不放**放大版的槽位印框——那会读成罩住人的玻璃板。
///
/// 场景挂在 <c>NCombatRoom.CombatVfxContainer</c>（角色之上），按目标身体的 hitbox 世界矩形定尺。
/// </summary>
[Tool]
public partial class PerformBodyVfx : Node2D
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
    private const string StampNodeName = "Stamp";
    private const string WashNodeName = "Wash";
    private const string CoreNodeName = "Core";
    private const string StreaksNodeName = "Streaks";
    private const string EmittersNodeName = "Emitters";

    /// <summary>设计基准：玩家角色的身体框（Sakiko 的 %Bounds 是 320×300），其余角色按比例换算。</summary>
    private static readonly Vector2 DesignBodySize = new(320f, 300f);

    // ── 共享时间线（五张牌一致，改这里等于改整套设计语言）──
    // 墨：自脚底升过身体、从头顶之上淡出。加法层在浅色背景上读不出来，
    // 这层 MIX 墨是"经过"的证据，同时把演奏读成一个自下而上的动作。
    private const float FlowStart = 0.04f;
    private const float FlowTravelTime = 0.26f;
    private const float FlowBandAlpha = 0.50f;
    private const float FlowBaseAlpha = 0.0f;
    private const float FlowEndTime = 0.40f;
    /// <summary>墨带在画布内的起止位置（纵向档 0 在底、1 在顶）。刻意的：不让墨带走到画布边缘，
    /// 否则会被 ColorRect 硬切成一条横杠；改为在腰以下聚起、在头顶之上淡掉。</summary>
    private const float FlowBandFrom = 0.18f;
    private const float FlowBandTo = 0.82f;

    private const float WashRiseTime = 0.10f;
    private const float WashEndTime = 0.34f;
    private const float WashPeakAlpha = 0.07f;
    private const float WashScaleGain = 0.24f;

    // 胸口落印：把同一枚印（同一 shader、同一斜切、与真实槽位同尺寸）按在奏者胸口，
    // 是"牌上落印"与"奏者响应"之间的同一性凭证——不是第二个峰值，只亮 0.2s。
    private const float StampStart = 0.10f;
    private const float StampDrawTime = 0.07f;
    private const float StampReleaseStart = 0.20f;
    private const float StampReleaseTime = 0.16f;
    private const float StampEndTime = 0.38f;

    // 落印：比槽位落印晚约 0.04s，读成"牌上落印 → 奏者响应"。
    private const float CoreRiseTime = 0.07f;
    private const float CorePeakTime = 0.14f;
    private const float CoreEndTime = 0.38f;
    private const float CorePeakAlpha = 0.95f;
    private const float CoreStartScale = 0.45f;
    private const float CorePulseGain = 0.42f;

    /// <summary>拍子脉冲的衰减时长。</summary>
    private const float PulseDecay = 0.14f;

    /// <summary>本体色：印框与墨迹的主色。</summary>
    [Export]
    public Color BodyTint { get; set; } = new("#ff8a34");

    /// <summary>高光色：胸口压点的白热点。</summary>
    [Export]
    public Color CoreTint { get; set; } = new("#fff4d8");

    /// <summary>辅色：墨迹笔尖的过渡色。</summary>
    [Export]
    public Color AccentTint { get; set; } = new("#d8402a");

    /// <summary>深色墨：MIX 混合升过身体的底色。</summary>
    [Export]
    public Color DeepTint { get; set; } = new("#b23a12");

    /// <summary>节拍时刻（秒，升序）。每次脉冲会点亮落印并发射对应拍的墨迹。</summary>
    [Export]
    public float[] Beats { get; set; } = [0.14f];

    /// <summary>墨带画布尺寸（设计基准下的像素）；比身体框高一截，墨带的推进范围收在画布内，不被边缘切出硬边。</summary>
    [Export]
    public Vector2 InkSize { get; set; } = new(250f, 500f);

    /// <summary>墨带画布中心（相对身体框中心的设计像素）。</summary>
    [Export]
    public Vector2 InkCenter { get; set; } = new(0f, -15f);

    /// <summary>胸口落印的尺寸（设计基准像素）；与槽位印框同量级，读成同一枚印。</summary>
    [Export]
    public Vector2 StampSize { get; set; } = new(108f, 48f);

    /// <summary>胸口落印的中心（相对身体框中心的设计像素）。</summary>
    [Export]
    public Vector2 StampCenter { get; set; } = new(0f, -10f);

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
    private ColorRect? _stamp;
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

    private Vector2 _bodySize = DesignBodySize;
    private Vector2 _bodyCenter;
    private bool _hasBodyAnchor;
    private Vector2 _bodyFit = Vector2.One;
    private float _sizeFit = 1f;
    private Tween? _tween;
    private int _nextBeat;

    public override void _Ready()
    {
        _flow = GetNodeOrNull<ColorRect>(FlowNodeName);
        _stamp = GetNodeOrNull<ColorRect>(StampNodeName);
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

        ApplyBodyFit();

        // 游戏内由调用方注入身体矩形；没注入（F6 单播）就按设计尺寸在视口中心播放。
        if (_hasBodyAnchor && !VfxPreviewSupport.IsPreview(this) && !Engine.IsEditorHint())
            GlobalPosition = _bodyCenter;

        if (VfxPreviewSupport.AutoPlayOnReady) Replay();
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

    /// <summary>
    /// 注入演奏者的身体矩形（世界坐标：中心 + 尺寸）；调用方在入树前设置。
    /// 典型来源是 <c>NCreature.Hitbox</c>——它就是各角色 %Bounds 换算出的世界矩形。
    /// </summary>
    public void ConfigureBody(Vector2 center, Vector2 size)
    {
        _bodySize = size is { X: > 0f, Y: > 0f } ? size : DesignBodySize;
        _bodyCenter = center;
        _hasBodyAnchor = true;
        ApplyBodyFit();
        if (IsInsideTree() && !VfxPreviewSupport.IsPreview(this)) GlobalPosition = _bodyCenter;
    }

    /// <summary>铺开几何：印框与墨贴合身体框并高出头顶，墨迹按比例散在身体与头顶周围。</summary>
    private void ApplyBodyFit()
    {
        // 编辑器里保留作者摆放。
        if (Engine.IsEditorHint())
            return;

        _bodyFit = new Vector2(
            Mathf.Max(_bodySize.X / DesignBodySize.X, 0.01f),
            Mathf.Max(_bodySize.Y / DesignBodySize.Y, 0.01f));
        _sizeFit = (_bodyFit.X + _bodyFit.Y) * 0.5f;

        var inkSize = new Vector2(
            Mathf.Max(InkSize.X * _bodyFit.X, 1f),
            Mathf.Max(InkSize.Y * _bodyFit.Y, 1f));
        var inkCenter = new Vector2(InkCenter.X * _bodyFit.X, InkCenter.Y * _bodyFit.Y);

        if (_flow != null)
        {
            _flow.Position = inkCenter - inkSize * 0.5f;
            _flow.Size = inkSize;
            _flow.SetInstanceShaderParameter(RectSizeParameter, inkSize);
        }

        if (_stamp != null)
        {
            var stampSize = new Vector2(
                Mathf.Max(StampSize.X * _bodyFit.X, 1f),
                Mathf.Max(StampSize.Y * _bodyFit.Y, 1f));
            var stampCenter = new Vector2(StampCenter.X * _bodyFit.X, StampCenter.Y * _bodyFit.Y);
            _stamp.Position = stampCenter - stampSize * 0.5f;
            _stamp.Size = stampSize;
            _stamp.SetInstanceShaderParameter(RectSizeParameter, stampSize);
        }

        for (var index = 0; index < _streaks.Length; index++)
        {
            _streaks[index].Position = _streakBasePositions[index] * _bodyFit;
        }

        for (var index = 0; index < _emitters.Length; index++)
        {
            var emitter = _emitters[index];
            emitter.Position = _emitterBasePositions[index] * _bodyFit;
            // 粒子材质是 local_to_scene，改它不会污染其他实例；编辑器已提前返回。
            if (emitter.ProcessMaterial is not ParticleProcessMaterial material) continue;
            if (material.EmissionShape != ParticleProcessMaterial.EmissionShapeEnum.Box) continue;
            var extents = _emitterBaseBoxExtents[index];
            material.EmissionBoxExtents = new Vector3(
                Mathf.Max(extents.X * _bodyFit.X, 1f),
                Mathf.Max(extents.Y * _bodyFit.Y, 1f),
                extents.Z);
        }
    }

    public void Replay()
    {
        _tween?.Kill();
        _tween = null;
        VfxPreviewSupport.CenterForPreview(this);
        ApplyBodyFit();
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
    private void ResetLayers()
    {
        _nextBeat = 0;
        if (_flow != null) _flow.Visible = false;
        if (_stamp != null)
        {
            _stamp.Visible = false;
            _stamp.SetInstanceShaderParameter(DrawProgressParameter, 0f);
            _stamp.SetInstanceShaderParameter(ReleaseProgressParameter, 0f);
        }
        if (_wash != null) _wash.Visible = false;
        if (_core != null) _core.Visible = false;
        foreach (var streak in _streaks) streak.Visible = false;
        foreach (var emitter in _emitters) emitter.Emitting = false;
    }

    /// <summary>单一时间线：所有层由 elapsed 求值，避免并行 tween 互相覆盖。</summary>
    private void ApplyTimeline(float elapsed)
    {
        ApplyFlow(elapsed);
        ApplyStamp(elapsed);
        ApplyWash(elapsed);
        ApplyCore(elapsed);
        ApplyStreaks(elapsed);
        ApplyBeats(elapsed);
    }

    /// <summary>升墨：墨带自脚底升到头顶之上，带身最重、全程留一层薄底；0.46s 前退干净。</summary>
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

    /// <summary>胸口落印：与槽位印框同一 shader 的横向档，自左向右划开、再向右溢出熄。</summary>
    private void ApplyStamp(float elapsed)
    {
        if (_stamp == null) return;
        if (elapsed >= StampEndTime)
        {
            _stamp.Visible = false;
            return;
        }

        var draw = Mathf.Clamp((elapsed - StampStart) / StampDrawTime, 0f, 1f);
        var release = Mathf.Clamp((elapsed - StampReleaseStart) / StampReleaseTime, 0f, 1f);
        var tint = BodyTint;
        tint.A = 0.7f;
        _stamp.Visible = draw > 0.001f;
        _stamp.SetInstanceShaderParameter(DrawProgressParameter, draw);
        _stamp.SetInstanceShaderParameter(ReleaseProgressParameter, release);
        _stamp.SetInstanceShaderParameter(EnergyParameter, 1f + 0.9f * ResolvePulse(elapsed));
        _stamp.SetInstanceShaderParameter(TintParameter, tint);
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
        _wash.Scale = _washBaseScale * _sizeFit * grow;
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
        _core.Scale = _coreBaseScale * _sizeFit * grow;
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
            streak.Position = _streakBasePositions[index] * _bodyFit +
                              new Vector2(streak.EndOffset.X * _bodyFit.X, streak.EndOffset.Y * _bodyFit.Y) *
                              EaseOut(progress);
            streak.Scale = _streakBaseScales[index] * _sizeFit *
                           Mathf.Lerp(1f, streak.EndScaleMultiplier, progress);
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

    private static float EaseInOut(float value)
    {
        var clamped = Mathf.Clamp(value, 0f, 1f);
        return clamped * clamped * (3f - 2f * clamped);
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

    // ── 触发：元素键 → 角色身上的身印场景 ──
    // 键与槽位印框共用（卡牌的 IPerformSlotVfxCard.PerformSlotVfxKey），
    // 于是同一张牌的两处反馈必然同色、同元素，不需要牌上再声明一次。

    private static readonly (string Key, string ScenePath)[] BodyScenes =
    [
        (PerformSlotVfxKeys.SymbolFire, "res://BangDreamInSpire/scenes/vfx/perform_body_fire_vfx.tscn"),
        (PerformSlotVfxKeys.SymbolWind, "res://BangDreamInSpire/scenes/vfx/perform_body_wind_vfx.tscn"),
        (PerformSlotVfxKeys.SymbolWater, "res://BangDreamInSpire/scenes/vfx/perform_body_water_vfx.tscn"),
        (PerformSlotVfxKeys.SymbolEarth, "res://BangDreamInSpire/scenes/vfx/perform_body_earth_vfx.tscn"),
        (PerformSlotVfxKeys.SymbolStars, "res://BangDreamInSpire/scenes/vfx/perform_body_stars_vfx.tscn")
    ];

    /// <summary>把五个身印加入战斗预载集，避免首次演奏时在主线程同步加载。</summary>
    public static void RegisterPreloads()
    {
        foreach (var (key, scenePath) in BodyScenes)
        {
            BangDreamPreloadManager.AddCustomCombatAsset($"perform_body_vfx_{key}", scenePath);
        }
    }

    private static string? ResolveScenePath(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        foreach (var (candidate, scenePath) in BodyScenes)
        {
            if (string.Equals(candidate, key, StringComparison.Ordinal)) return scenePath;
        }

        return null;
    }

    /// <summary>
    /// 在演奏者身上播放该牌的身印；未登记元素、演出节点缺失或战斗房间不可用时静默跳过
    /// ——身印是加成表现，缺了不能影响演奏结算。
    /// </summary>
    public static void TryPlay(CardModel card)
    {
        if (Engine.IsEditorHint()) return;
        if (card is not IPerformSlotVfxCard vfxCard) return;
        if (ResolveScenePath(vfxCard.PerformSlotVfxKey) is not { } scenePath) return;
        if (NCombatRoom.Instance is not { } room) return;
        if (card.Owner?.Creature is not { } creature) return;

        var creatureNode = room.GetCreatureNode(creature);
        if (creatureNode == null || !IsInstanceValid(creatureNode)) return;

        var size = creatureNode.Hitbox.Size;
        if (size is not { X: > 0f, Y: > 0f }) return;
        if (!ResourceLoader.Exists(scenePath)) return;

        var vfx = BangDreamPreloadManager.GetScene(scenePath).Instantiate<PerformBodyVfx>();
        vfx.ConfigureBody(creatureNode.Hitbox.GlobalPosition + size * 0.5f, size);
        room.CombatVfxContainer.AddChildSafely(vfx);
    }
}
