using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamLib.Scripts.Nodes.VFX;

/// <summary>
/// 波点律动攻击特效：节拍（BPM）驱动的波点阵主层 → 三拍递进的波点环 → 第三拍命中爆发
/// （闪光 / 扩散环 / 冲击波 / 波点·音符·星点粒子）。
/// 所有元素都是同一套波点语言：主题色柔光点 + 加法混合，不引入异质色的贴图装饰。
/// 编辑器里保持作者摆放的静止状态，按 F6 或勾选 Inspector 的 Play 才播放。
/// </summary>
/// <remarks>
/// 全程不使用 Tween：所有状态由 <see cref="ApplyState" /> 按 <c>_elapsed</c> 单点求值，
/// 既避开并行 Tween 对同一属性互相覆盖的坑，也让「第几拍发生什么」一眼可读。
/// </remarks>
[Tool]
public partial class PolkaBeatVfx : Node2D
{
    // ── 着色器参数名 ──
    private static readonly StringName ParamPatternWeight = "pattern_weight";
    private static readonly StringName ParamCells = "cells";
    private static readonly StringName ParamDotRadius = "dot_radius";
    private static readonly StringName ParamWaveRadius = "wave_radius";
    private static readonly StringName ParamWaveStrength = "wave_strength";
    private static readonly StringName ParamBeatPulse = "beat_pulse";
    private static readonly StringName ParamScatter = "scatter";
    private static readonly StringName ParamRevealRadius = "reveal_radius";
    private static readonly StringName ParamFieldAlpha = "field_alpha";
    private static readonly StringName ParamBrightness = "brightness";
    private static readonly StringName ParamTint = "tint";

    /// <summary>点阵场淡入时长。</summary>
    private const float FieldFadeIn = 0.06f;

    /// <summary>单个波点环的存活时长 = 一拍 × 该系数（略短于一拍，让环之间有间隙）。</summary>
    private const float RingLifeRatio = 0.9f;

    /// <summary>同一环内相邻点的错帧量，形成顺时针扫入的观感。</summary>
    private const float RingStagger = 0.012f;

    /// <summary>环外扩过程中的整体旋转（弧度）。</summary>
    private const float RingSwirl = 0.25f;

    /// <summary>命中后点阵向外飞散到位的时间。</summary>
    private const float HitScatterTime = 0.28f;

    /// <summary>
    /// 点阵场扩张前沿的起始半径（UV）。不能取太小：前沿要明显大于一个格子
    /// （cells=13 → 每格 512/13 ≈ 39px，即 UV 0.077），否则起播瞬间与编辑器里
    /// 都只剩一个「点都没有」的空场。
    /// </summary>
    private const float RevealStart = 0.14f;

    /// <summary>点阵场扩张前沿在命中时刻的半径（UV）。</summary>
    private const float RevealAtHit = 0.44f;

    /// <summary>点阵场扩张前沿在收尾时的半径（UV，再大就会被画布边界切方）。</summary>
    private const float RevealEnd = 0.48f;

    /// <summary>波前亮带相对扩张前沿的内缩量：亮带贴着前沿，读作「扩张的边缘」。</summary>
    private const float WaveLeadOffset = 0.05f;

    /// <summary>波点波形带的波峰宽度（距中心归一化距离）。</summary>
    private const float WaveCrestWidth = 0.26f;

    private const float ShockTime = 0.30f;
    private const float ExpandTime = 0.36f;
    private const float ExpandFadeStart = 0.24f;
    private const float ExpandFadeTime = 0.12f;
    private const float CoreTime = 0.10f;

    /// <summary>命中瞬间律动核炸开成波点环的时长。</summary>
    private const float CoreBurstTime = 0.24f;

    private const int ExpandRingFrames = 9;

    private Node2D _dotField = null!;
    private Node2D _beatWave = null!;
    private Node2D _beatRings = null!;
    private Node2D _beatCore = null!;
    private Node2D _impactGroup = null!;
    private Sprite2D _shockRing = null!;
    private Sprite2D _expandRing = null!;
    private Sprite2D _coreFlash = null!;
    private GpuParticles2D _dotParticles = null!;
    private GpuParticles2D _starParticles = null!;

    private ShaderMaterial? _fieldMaterial;
    private ParticleProcessMaterial? _dotParticleMaterial;

    // 波点波形带：一条横排的波点，每拍从中心向两端推一个波峰。
    private Sprite2D[] _waveDots = null!;
    private float[] _waveDotOffset = null!;
    private Vector2[] _waveDotBaseScale = null!;
    private Color[] _waveDotBaseModulate = null!;

    // 律动核：命中前在中心逐拍搏动，命中瞬间被闪光吞掉。
    private Sprite2D[] _coreDots = null!;
    private float[] _coreDotAngle = null!;
    private Vector2[] _coreDotBaseScale = null!;
    private Color[] _coreDotBaseModulate = null!;
    private float _coreBaseRadius;

    // 每个波点环：节点、环上的点、各点相对环心的极角与美术基准值。
    private Node2D[] _rings = null!;
    private Sprite2D[][] _ringDots = null!;
    private float[][] _ringDotAngle = null!;
    private Vector2[][] _ringDotBaseScale = null!;
    private Color[][] _ringDotBaseModulate = null!;
    private float[] _ringStartRadius = null!;

    // 从场景读回的美术基准值，重播时按此复位（不写死初值）。
    private Vector2 _shockRingBaseScale;
    private Vector2 _expandRingBaseScale;
    private Vector2 _coreFlashBaseScale;

    private float _elapsed;
    private bool _active;
    private bool _particlesFired;
    private bool _initialized;

    /// <summary>本次播放的主题色，逐帧写进着色器 tint（alpha 位置换成整段包络）。</summary>
    private Color _tint = Colors.White;

    // ── 节奏 ──
    private float _beatBpm = 210f;
    private int _hitBeat = 3;
    private float _tailTime = 0.45f;

    // ── 波点环 ──
    private float _ringExpand = 2.2f;

    // ── 着色器可调项 ──
    private float _fieldCells = 13f;
    private float _dotRadius = 0.17f;
    private float _patternWeight = 0.55f;
    private float _waveStrength = 1.8f;

    /// <summary>每分钟节拍数，决定整段特效的节奏快慢。</summary>
    [Export(PropertyHint.Range, "60,400,1")]
    public float BeatBpm
    {
        get => _beatBpm;
        set
        {
            _beatBpm = value;
            ReplayIfPreviewRun();
        }
    }

    /// <summary>第几拍命中（1 起）。波点环在第 1..HitBeat 拍依次触发，最后一拍与命中同帧。</summary>
    [Export(PropertyHint.Range, "1,8,1")]
    public int HitBeat
    {
        get => _hitBeat;
        set
        {
            _hitBeat = value;
            ReplayIfPreviewRun();
        }
    }

    /// <summary>命中后的收尾时长（淡出、点阵飞散、粒子存活都在这一段里）。</summary>
    [Export(PropertyHint.Range, "0.1,2.0,0.01")]
    public float TailTime
    {
        get => _tailTime;
        set
        {
            _tailTime = value;
            ReplayIfPreviewRun();
        }
    }

    /// <summary>波点环外扩到的半径倍率（相对作者摆放的起始半径）。</summary>
    [Export(PropertyHint.Range, "1.2,4.0,0.1")]
    public float RingExpand
    {
        get => _ringExpand;
        set
        {
            _ringExpand = value;
            ReplayIfPreviewRun();
        }
    }

    /// <summary>点阵密度：每边格子数。</summary>
    [Export(PropertyHint.Range, "2,32,1")]
    public float FieldCells
    {
        get => _fieldCells;
        set
        {
            _fieldCells = value;
            ReplayIfPreviewRun();
        }
    }

    /// <summary>单个波点半径（格子内归一化，0.5 即填满格子）。</summary>
    [Export(PropertyHint.Range, "0.02,0.5,0.01")]
    public float DotRadius
    {
        get => _dotRadius;
        set
        {
            _dotRadius = value;
            ReplayIfPreviewRun();
        }
    }

    /// <summary>库网点贴图在点阵遮罩里的权重（0 = 纯程序化点阵）。</summary>
    [Export(PropertyHint.Range, "0.0,1.0,0.05")]
    public float PatternWeight
    {
        get => _patternWeight;
        set
        {
            _patternWeight = value;
            ReplayIfPreviewRun();
        }
    }

    /// <summary>波前对点的放大加亮强度。</summary>
    [Export(PropertyHint.Range, "0.0,3.0,0.1")]
    public float WaveStrength
    {
        get => _waveStrength;
        set
        {
            _waveStrength = value;
            ReplayIfPreviewRun();
        }
    }

    /// <summary>预览时使用的固定着色；<see cref="RandomizeTint" /> 为 true 时忽略。</summary>
    [Export] public Color PreviewTint { get; set; } = new("#7799CC");

    /// <summary>预览时是否随机取一个角色主题色着色。</summary>
    [Export] public bool RandomizeTint { get; set; }

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

    /// <summary>预览态下播放结束后自动重播，方便反复打磨手感。</summary>
    [Export] public bool LoopPreview { get; set; } = true;

    /// <summary>一拍时长（秒）。</summary>
    private float Beat => 60f / Mathf.Max(1f, _beatBpm);

    /// <summary>命中时刻（秒）。波点环在第 1..HitBeat 拍依次触发，故命中落在第 HitBeat 拍。</summary>
    private float HitTime => (Mathf.Max(1, _hitBeat) - 1) * Beat;

    /// <summary>整段时长。</summary>
    private float TotalTime => HitTime + Mathf.Max(0.05f, _tailTime);

    /// <summary>
    /// 每拍把扩张前沿向外推多远：由 <see cref="HitTime" /> 反推，
    /// 保证无论 HitBeat 设成几，前沿都恰好在命中那一拍抵达 <see cref="RevealAtHit" />。
    /// </summary>
    private float RevealPerBeat => (RevealAtHit - RevealStart) / Mathf.Max(1, _hitBeat - 1);

    public override void _Ready()
    {
        _dotField = GetNode<Node2D>("DotField");
        _beatWave = GetNode<Node2D>("BeatWave");
        _beatRings = GetNode<Node2D>("BeatRings");
        _beatCore = GetNode<Node2D>("BeatCore");
        _impactGroup = GetNode<Node2D>("ImpactGroup");
        _shockRing = GetNode<Sprite2D>("ImpactGroup/ShockRing");
        _expandRing = GetNode<Sprite2D>("ImpactGroup/ExpandRing");
        _coreFlash = GetNode<Sprite2D>("ImpactGroup/CoreFlash");
        _dotParticles = GetNode<GpuParticles2D>("DotParticles");
        _starParticles = GetNode<GpuParticles2D>("StarParticles");

        _fieldMaterial = _dotField.Material as ShaderMaterial;
        _dotParticleMaterial = _dotParticles.ProcessMaterial as ParticleProcessMaterial;

        // 波点波形带：各点距中心的归一化距离由作者摆放的 x 反解（中心点 = 0，两端 = 1）。
        var waveCount = _beatWave.GetChildCount();
        _waveDots = new Sprite2D[waveCount];
        _waveDotOffset = new float[waveCount];
        _waveDotBaseScale = new Vector2[waveCount];
        _waveDotBaseModulate = new Color[waveCount];
        var maxOffset = 0f;
        for (var j = 0; j < waveCount; j++)
        {
            var dot = _beatWave.GetChild<Sprite2D>(j);
            _waveDots[j] = dot;
            _waveDotOffset[j] = Mathf.Abs(dot.Position.X);
            _waveDotBaseScale[j] = dot.Scale;
            _waveDotBaseModulate[j] = dot.Modulate;
            maxOffset = Mathf.Max(maxOffset, _waveDotOffset[j]);
        }

        for (var j = 0; j < waveCount; j++)
            _waveDotOffset[j] = maxOffset > 0f ? _waveDotOffset[j] / maxOffset : 0f;

        // 律动核：作者摆放的极坐标就是「搏动基准半径 + 起始角度」。
        var coreCount = _beatCore.GetChildCount();
        _coreDots = new Sprite2D[coreCount];
        _coreDotAngle = new float[coreCount];
        _coreDotBaseScale = new Vector2[coreCount];
        _coreDotBaseModulate = new Color[coreCount];
        var coreRadiusSum = 0f;
        for (var j = 0; j < coreCount; j++)
        {
            var dot = _beatCore.GetChild<Sprite2D>(j);
            _coreDots[j] = dot;
            _coreDotAngle[j] = dot.Position.Angle();
            _coreDotBaseScale[j] = dot.Scale;
            _coreDotBaseModulate[j] = dot.Modulate;
            coreRadiusSum += dot.Position.Length();
        }

        _coreBaseRadius = coreCount > 0 ? coreRadiusSum / coreCount : 0f;

        // 波点环：环数与每环点数由场景结构决定，加一个 Ring3 不需要改代码。
        var ringCount = _beatRings.GetChildCount();
        _rings = new Node2D[ringCount];
        _ringDots = new Sprite2D[ringCount][];
        _ringDotAngle = new float[ringCount][];
        _ringDotBaseScale = new Vector2[ringCount][];
        _ringDotBaseModulate = new Color[ringCount][];
        _ringStartRadius = new float[ringCount];

        for (var i = 0; i < ringCount; i++)
        {
            var ring = _beatRings.GetChild<Node2D>(i);
            var dotCount = ring.GetChildCount();
            _rings[i] = ring;
            _ringDots[i] = new Sprite2D[dotCount];
            _ringDotAngle[i] = new float[dotCount];
            _ringDotBaseScale[i] = new Vector2[dotCount];
            _ringDotBaseModulate[i] = new Color[dotCount];

            var radiusSum = 0f;
            for (var j = 0; j < dotCount; j++)
            {
                var dot = ring.GetChild<Sprite2D>(j);
                _ringDots[i][j] = dot;
                // 作者摆放的极坐标就是「起点角度 + 起始半径」，重播时据此复位。
                _ringDotAngle[i][j] = dot.Position.Angle();
                _ringDotBaseScale[i][j] = dot.Scale;
                _ringDotBaseModulate[i][j] = dot.Modulate;
                radiusSum += dot.Position.Length();
            }

            _ringStartRadius[i] = dotCount > 0 ? radiusSum / dotCount : 0f;
        }

        // 只读回基准值，不写任何视觉状态，编辑器里场景保持静止。
        _shockRingBaseScale = _shockRing.Scale;
        _expandRingBaseScale = _expandRing.Scale;
        _coreFlashBaseScale = _coreFlash.Scale;

        _initialized = true;

        if (VfxPreviewSupport.AutoPlayOnReady)
            Replay();
    }

    /// <summary>重置并重新播放一次特效。</summary>
    public void Replay()
    {
        if (!_initialized)
            return;

        _elapsed = 0f;
        _active = true;
        _particlesFired = false;

        // 预览时摆到视口中心，游戏内位置由调用方决定。
        VfxPreviewSupport.CenterForPreview(this);

        _tint = RandomizeTint ? VfxPreviewSupport.RandomCharacterColor(this) : PreviewTint;

        // 主题色只上到点阵场、波点环、波形带与律动核，命中爆发层保持白色。
        _dotField.Modulate = Colors.White;
        var dotTint = new Color(_tint.R, _tint.G, _tint.B, 1f);
        foreach (var ring in _rings)
            ring.Modulate = dotTint;
        _beatWave.Modulate = dotTint;
        _beatCore.Modulate = dotTint;

        foreach (var dots in _ringDots)
        foreach (var dot in dots)
            dot.Visible = false;

        for (var j = 0; j < _waveDots.Length; j++)
        {
            _waveDots[j].Scale = _waveDotBaseScale[j];
            _waveDots[j].Modulate = _waveDotBaseModulate[j];
        }

        for (var j = 0; j < _coreDots.Length; j++)
        {
            _coreDots[j].Position =
                new Vector2(Mathf.Cos(_coreDotAngle[j]), Mathf.Sin(_coreDotAngle[j])) * _coreBaseRadius;
            _coreDots[j].Scale = _coreDotBaseScale[j];
            _coreDots[j].Modulate = _coreDotBaseModulate[j];
        }

        _impactGroup.Visible = false;
        _shockRing.Scale = _shockRingBaseScale;
        _shockRing.Modulate = Colors.White;
        _expandRing.Frame = 0;
        _expandRing.Scale = _expandRingBaseScale;
        _expandRing.Modulate = Colors.White;
        _coreFlash.Scale = _coreFlashBaseScale;
        _coreFlash.Modulate = Colors.White;

        _dotParticles.Emitting = false;
        _starParticles.Emitting = false;

        ApplyShaderParams();
        ApplyParticleTint();
        ApplyState(0f);
    }

    public override void _Process(double delta)
    {
        if (!_active)
            return;

        if (!VfxPreviewSupport.ShouldContinuePlayback(this))
        {
            _active = false;
            return;
        }

        _elapsed += (float)delta;
        ApplyState(_elapsed);

        if (_elapsed < TotalTime)
            return;

        _active = false;

        if (VfxPreviewSupport.ShouldSelfFree(this))
        {
            this.QueueFreeSafely();
            return;
        }

        if (LoopPreview)
            Replay();
    }

    /// <summary>
    /// 把 <paramref name="elapsed" /> 时刻的全部视觉状态一次算出来。
    /// 这是唯一写视觉状态的地方，节拍结构因此可以按时间线性阅读。
    /// </summary>
    private void ApplyState(float elapsed)
    {
        var beat = Beat;
        var hitTime = HitTime;
        var tail = Mathf.Max(0.05f, _tailTime);

        var beforeHit = elapsed < hitTime;
        // 命中后进度：0（命中帧）→ 1（整段结束）。
        var afterHit = Mathf.Clamp((elapsed - hitTime) / tail, 0f, 1f);

        // ── 点阵场 ──
        // 拍脉冲：拍点最强，拍内三次方衰减；命中后换成一次收尾脉冲。
        var beatPulse = beforeHit
            ? Mathf.Pow(1f - elapsed / beat % 1f, 3f)
            : Mathf.Pow(1f - afterHit, 2f);

        // 包络：起步淡入，命中后随收尾淡出。
        var envelope = beforeHit
            ? Mathf.Min(elapsed / FieldFadeIn, 1f)
            : 1f - afterHit * afterHit;

        // 扩张前沿：每拍向外推一截（拍内 EaseOutQuad 先快后慢），
        // 与波点环、波峰同源，因此「扩张」也踩在节拍上；命中后继续外扩并淡出。
        var beatFrac = elapsed / beat % 1f;
        var revealFront = beforeHit
            ? RevealStart + ((int)(elapsed / beat) + EaseOutQuad(beatFrac)) * RevealPerBeat
            : Mathf.Lerp(RevealAtHit, RevealEnd, Mathf.Min(afterHit * 2f, 1f));

        // 整场不透明度随拍呼吸：拍点最实、拍内回落。
        // 扩张靠这条透明度前沿 + 呼吸，不再靠把整块贴图缩放（缩放只会让点一起变大）。
        var fieldAlpha = 0.7f + 0.3f * beatPulse;

        var brightness = beforeHit
            ? 1f + 0.35f * beatPulse
            : Mathf.Lerp(2.6f, 0.8f, afterHit);

        SetShaderParam(ParamBeatPulse, beatPulse);
        SetShaderParam(ParamRevealRadius, revealFront);
        SetShaderParam(ParamFieldAlpha, fieldAlpha);
        // 波前亮带贴着扩张前沿的内侧，读作「正在扩张的边缘」。
        SetShaderParam(ParamWaveRadius, revealFront - WaveLeadOffset);
        SetShaderParam(ParamScatter,
            beforeHit ? 0f : Mathf.Clamp((elapsed - hitTime) / HitScatterTime, 0f, 1f));
        SetShaderParam(ParamBrightness, brightness);
        SetShaderParam(ParamTint, new Color(_tint.R, _tint.G, _tint.B, envelope));

        // ── 波点环 ──
        for (var i = 0; i < _rings.Length; i++)
        {
            var ringStart = i * beat;
            var dots = _ringDots[i];
            var startRadius = _ringStartRadius[i];

            for (var j = 0; j < dots.Length; j++)
            {
                var dot = dots[j];
                var local = elapsed - ringStart - j * RingStagger;
                var progress = local / (beat * RingLifeRatio);

                if (local <= 0f || progress >= 1f)
                {
                    dot.Visible = false;
                    continue;
                }

                progress = Mathf.Clamp(progress, 0f, 1f);
                var eased = EaseOutQuad(progress);
                var radius = Mathf.Lerp(startRadius, startRadius * _ringExpand, eased);
                var angle = _ringDotAngle[i][j] + RingSwirl * progress;

                dot.Position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                dot.Scale = _ringDotBaseScale[i][j] * Mathf.Lerp(0.55f, 1.2f, eased);

                var baseModulate = _ringDotBaseModulate[i][j];
                dot.Modulate = new Color(baseModulate.R, baseModulate.G, baseModulate.B,
                    baseModulate.A * Mathf.Sin(Mathf.Pi * progress));
                dot.Visible = true;
            }
        }

        // ── 波点波形带 ──
        // 每拍从中心向两端推一个波峰：波峰处的点被放大提亮，读作「跟着节拍走的音波」。
        // 命中前 0.18s 起收掉，把画面完全让给爆发层。
        var beatProgress = elapsed / beat % 1f;
        var waveFade = Mathf.Clamp((hitTime - elapsed) / 0.18f, 0f, 1f) * envelope;
        for (var j = 0; j < _waveDots.Length; j++)
        {
            var dot = _waveDots[j];
            var crest = Mathf.Exp(-Mathf.Pow((_waveDotOffset[j] - beatProgress) / WaveCrestWidth, 2f));
            var baseModulate = _waveDotBaseModulate[j];

            dot.Scale = _waveDotBaseScale[j] * Mathf.Lerp(0.6f, 1.5f, crest);
            dot.Modulate = new Color(baseModulate.R, baseModulate.G, baseModulate.B,
                baseModulate.A * Mathf.Lerp(0.12f, 1f, crest) * waveFade);
        }

        // ── 律动核 ──
        // 命中前在中心逐拍搏动（读作心跳）；命中瞬间炸开成一个波点环接住这一拍。
        // 它是整套特效里唯一「命中时从中心长出波点」的元素，取代了原先那枚金色谱号装饰。
        float coreScale;
        float coreAlpha;
        if (beforeHit)
        {
            coreScale = 1f + 0.5f * beatPulse;
            coreAlpha = (0.35f + 0.65f * beatPulse) * envelope;
        }
        else
        {
            var burst = Mathf.Clamp((elapsed - hitTime) / CoreBurstTime, 0f, 1f);
            coreScale = Mathf.Lerp(1.5f, 4.2f, EaseOutQuad(burst));
            coreAlpha = 1f - burst * burst;
        }

        for (var j = 0; j < _coreDots.Length; j++)
        {
            var dot = _coreDots[j];
            var baseModulate = _coreDotBaseModulate[j];

            dot.Position = new Vector2(Mathf.Cos(_coreDotAngle[j]), Mathf.Sin(_coreDotAngle[j])) *
                           (_coreBaseRadius * coreScale);
            dot.Scale = _coreDotBaseScale[j] * coreScale;
            dot.Modulate = new Color(baseModulate.R, baseModulate.G, baseModulate.B,
                baseModulate.A * coreAlpha);
        }

        // ── 命中爆发 ──
        if (beforeHit)
        {
            _impactGroup.Visible = false;
            return;
        }

        _impactGroup.Visible = true;

        // 冲击波：向外扩并淡出。
        var shockProgress = Mathf.Clamp((elapsed - hitTime) / ShockTime, 0f, 1f);
        _shockRing.Scale = _shockRingBaseScale * Mathf.Lerp(0.35f, 2.2f, EaseOutQuad(shockProgress));
        _shockRing.Modulate = new Color(1f, 1f, 1f, 1f - shockProgress);

        // 扩散环：9 帧序列帧推进，尾段淡出。
        var expandProgress = Mathf.Clamp((elapsed - hitTime) / ExpandTime, 0f, 1f);
        _expandRing.Frame = FrameAt(expandProgress, ExpandRingFrames);
        _expandRing.Scale = _expandRingBaseScale * Mathf.Lerp(1f, 1.6f, EaseOutQuad(expandProgress));
        _expandRing.Modulate = new Color(1f, 1f, 1f,
            1f - Mathf.Clamp((elapsed - hitTime - ExpandFadeStart) / ExpandFadeTime, 0f, 1f));

        // 命中闪光：单帧星芒闪现后急速淡出。
        var coreProgress = Mathf.Clamp((elapsed - hitTime) / CoreTime, 0f, 1f);
        _coreFlash.Scale = _coreFlashBaseScale * Mathf.Lerp(0.6f, 1.4f, EaseOutQuad(coreProgress));
        _coreFlash.Modulate = new Color(1f, 1f, 1f, 1f - coreProgress);

        if (_particlesFired)
            return;

        _particlesFired = true;
        _dotParticles.Restart();
        _starParticles.Restart();
    }

    /// <summary>把不随时间的着色器参数一次性写进材质（重播时调用）。</summary>
    private void ApplyShaderParams()
    {
        SetShaderParam(ParamCells, _fieldCells);
        SetShaderParam(ParamDotRadius, _dotRadius);
        SetShaderParam(ParamPatternWeight, _patternWeight);
        SetShaderParam(ParamWaveStrength, _waveStrength);
        SetShaderParam(ParamTint, _tint);
    }

    /// <summary>
    /// 波点粒子跟随主题色。编辑器里写「实例级」的 <c>SelfModulate</c>，
    /// 游戏内写粒子材质的 color——与着色参数同样的取舍（见 <see cref="SetShaderParam" />）。
    /// </summary>
    private void ApplyParticleTint()
    {
        var dotColor = new Color(_tint.R, _tint.G, _tint.B, 1f);

        if (Engine.IsEditorHint())
            _dotParticles.SelfModulate = dotColor;
        else if (_dotParticleMaterial != null)
            _dotParticleMaterial.Color = dotColor;
    }

    /// <summary>
    /// 编辑器里写「实例级」着色参数，避免逐帧改动场景共享的 ShaderMaterial 而把场景标记为已修改；
    /// 游戏内沿用写共享材质的写法。
    /// </summary>
    private void SetShaderParam(StringName name, Variant value)
    {
        if (Engine.IsEditorHint())
            _dotField.SetInstanceShaderParameter(name, value);
        else
            _fieldMaterial?.SetShaderParameter(name, value);
    }

    /// <summary>只在「F6 单独运行」时响应参数改动而重播；编辑器里不重播。</summary>
    private void ReplayIfPreviewRun()
    {
        if (_initialized && VfxPreviewSupport.IsPreviewRun(this))
            Replay();
    }

    /// <summary>Quad-Out 缓动：起步快、末端收住。</summary>
    private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

    /// <summary>把归一化进度映射到 0..frameCount-1 的帧号。</summary>
    private static int FrameAt(float progress, int frameCount) =>
        Mathf.Clamp((int)(progress * frameCount), 0, frameCount - 1);
}