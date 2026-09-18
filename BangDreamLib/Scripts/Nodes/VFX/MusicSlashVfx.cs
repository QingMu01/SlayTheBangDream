using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamLib.Scripts.Nodes.VFX;

/// <summary>
/// 音乐主题演奏斩击：月牙弧交叉扫过 → 命中星芒与扩散环 → 谱号装饰闪亮消隐 → 音符/星屑粒子。
/// 编辑器里保持作者摆放的静止状态，按 F6 或勾选 Inspector 的 Play 才播放。
/// </summary>
[Tool]
public partial class MusicSlashVfx : Node2D
{
    /// <summary>单条斩击弧扫过的时间。</summary>
    private const float SlashDuration = 0.18f;

    /// <summary>命中判定帧，命中信号在此发出。</summary>
    private const float HitTime = 0.14f;

    /// <summary>副斩相对主斩的错帧量，两弧错开交叉切过。</summary>
    private const float SubSlashDelay = 0.08f;

    /// <summary>拖影相对本体的延迟，越短拖尾越紧凑。</summary>
    private const float GhostDelay = 0.035f;

    /// <summary>整段特效时长，与音符粒子存活期对齐。</summary>
    private const float TotalDuration = 0.86f;

    /// <summary>谱号装饰亮起后的峰值不透明度。压低以弱化生硬的实心剪影。</summary>
    private const float CrestPeakAlpha = 0.85f;

    /// <summary>谱号从透明弹入的时间。</summary>
    private const float CrestRiseTime = 0.10f;

    /// <summary>谱号保持在峰值不透明度的时间。</summary>
    private const float CrestHoldTime = 0.16f;

    /// <summary>谱号扩散消隐的时间。</summary>
    private const float CrestFadeTime = 0.22f;

    /// <summary>谱号装饰的完整存活时长，由单一 TweenMethod 覆盖。</summary>
    private const float CrestLifeTime = CrestRiseTime + CrestHoldTime + CrestFadeTime;

    private const int ExpandRingFrames = 9;

    /// <summary>斩击弧扫过的角度范围（度）：起点落后基准角，终点略越过基准角。</summary>
    private const float SlashSweepDegrees = 75f;

    /// <summary>斩击弧越过基准角后的收尾角度（度）。</summary>
    private const float SlashOvershootDegrees = 12f;

    /// <summary>斩击弧从画面外扫入，终点越过命中点，读作「划过去」而非「挪过来」。</summary>
    private static readonly Vector2 SlashMainStart = new(-86f, -54f);
    private static readonly Vector2 SlashMainEnd = new(18f, 12f);
    private static readonly Vector2 SlashSubStart = new(86f, 54f);
    private static readonly Vector2 SlashSubEnd = new(-18f, -12f);

    /// <summary>扫过过程中弧线的缩放倍率：由小放大，强化方向感。</summary>
    private static readonly Vector2 SlashStartScaleMul = new(0.78f, 0.78f);
    private static readonly Vector2 SlashEndScaleMul = new(1.25f, 1.25f);

    /// <summary>拖影相对本体的缩放倍率。</summary>
    private static readonly Vector2 GhostScaleMul = new(0.88f, 0.88f);

    private Node2D _slashGroup = null!;
    private Sprite2D _slashMain = null!;
    private Sprite2D _slashSub = null!;
    private Sprite2D _slashMainGhost = null!;
    private Sprite2D _slashSubGhost = null!;
    private Node2D _impactGroup = null!;
    private Sprite2D _shockRing = null!;
    private Sprite2D _expandRing = null!;
    private Sprite2D _coreFlash = null!;
    private Sprite2D _staffCrest = null!;
    private Sprite2D _critOverlay = null!;
    private GpuParticles2D _noteParticles = null!;
    private GpuParticles2D _starParticles = null!;

    // 从场景读回的美术基准值，重播时按此复位（不写死初值）。
    private Vector2 _slashMainBaseScale;
    private Vector2 _slashSubBaseScale;
    private Vector2 _shockRingBaseScale;
    private Vector2 _expandRingBaseScale;
    private Vector2 _coreFlashBaseScale;
    private Vector2 _staffCrestBaseScale;
    private float _slashMainBaseRotation;
    private float _slashSubBaseRotation;
    private Color _ghostBaseModulate;

    private Tween? _tween;

    /// <summary>预览时使用的固定着色；<see cref="RandomizeTint" /> 为 true 时忽略。</summary>
    [Export] public Color PreviewTint { get; set; } = new("#7799CC");

    /// <summary>预览时是否随机取一个角色主题色着色。</summary>
    [Export] public bool RandomizeTint { get; set; }

    /// <summary>暴击变体叠加层是否可见；调用方按暴击结果设置，预览时手动勾选。</summary>
    [Export] public bool IsCritical { get; set; }

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

    public override void _Ready()
    {
        _slashGroup = GetNode<Node2D>("SlashGroup");
        _slashMain = GetNode<Sprite2D>("SlashGroup/SlashMain");
        _slashSub = GetNode<Sprite2D>("SlashGroup/SlashSub");
        _slashMainGhost = GetNode<Sprite2D>("SlashGroup/SlashMainGhost");
        _slashSubGhost = GetNode<Sprite2D>("SlashGroup/SlashSubGhost");
        _impactGroup = GetNode<Node2D>("ImpactGroup");
        _shockRing = GetNode<Sprite2D>("ImpactGroup/ShockRing");
        _expandRing = GetNode<Sprite2D>("ImpactGroup/ExpandRing");
        _coreFlash = GetNode<Sprite2D>("ImpactGroup/CoreFlash");
        _staffCrest = GetNode<Sprite2D>("ImpactGroup/StaffCrest");
        _critOverlay = GetNode<Sprite2D>("ImpactGroup/CritOverlay");
        _noteParticles = GetNode<GpuParticles2D>("NoteParticles");
        _starParticles = GetNode<GpuParticles2D>("StarParticles");

        // 只读回基准值，不写任何视觉状态，编辑器里场景保持静止。
        _slashMainBaseScale = _slashMain.Scale;
        _slashSubBaseScale = _slashSub.Scale;
        _shockRingBaseScale = _shockRing.Scale;
        _expandRingBaseScale = _expandRing.Scale;
        _coreFlashBaseScale = _coreFlash.Scale;
        _staffCrestBaseScale = _staffCrest.Scale;
        _slashMainBaseRotation = _slashMain.Rotation;
        _slashSubBaseRotation = _slashSub.Rotation;
        _ghostBaseModulate = _slashMainGhost.Modulate;

        if (VfxPreviewSupport.AutoPlayOnReady)
            Replay();
    }

    /// <summary>重置并重新播放一次特效。</summary>
    public void Replay()
    {
        _tween?.Kill();

        // 预览时摆到视口中心，游戏内位置由调用方决定。
        VfxPreviewSupport.CenterForPreview(this);

        var tint = RandomizeTint ? VfxPreviewSupport.RandomCharacterColor(this) : PreviewTint;

        // 主题色只上到斩击层，命中爆发层保持白色。
        _slashGroup.Visible = true;
        _slashGroup.Modulate = new Color(tint.R, tint.G, tint.B, 1f);
        ResetSlashSprite(_slashMain, SlashMainStart, _slashMainBaseScale, _slashMainBaseRotation, Colors.White);
        ResetSlashSprite(_slashSub, SlashSubStart, _slashSubBaseScale, _slashSubBaseRotation, Colors.White);
        ResetSlashSprite(_slashMainGhost, SlashMainStart, _slashMainBaseScale,
            _slashMainBaseRotation, _ghostBaseModulate);
        ResetSlashSprite(_slashSubGhost, SlashSubStart, _slashSubBaseScale,
            _slashSubBaseRotation, _ghostBaseModulate);

        _impactGroup.Visible = false;
        _impactGroup.Modulate = Colors.White;
        _shockRing.Scale = _shockRingBaseScale;
        _shockRing.Modulate = Colors.White;
        _expandRing.Frame = 0;
        _expandRing.Scale = _expandRingBaseScale;
        _expandRing.Modulate = Colors.White;
        _coreFlash.Scale = _coreFlashBaseScale;
        _coreFlash.Modulate = Colors.White;
        _staffCrest.Scale = _staffCrestBaseScale;
        _staffCrest.Modulate = Colors.White;
        _critOverlay.Visible = IsCritical;

        _noteParticles.Emitting = false;
        _starParticles.Emitting = false;

        _tween = CreateTween();
        _tween.SetParallel(true);
        BuildSlashStage(_tween);
        BuildImpactStage(_tween);
        _tween.TweenCallback(Callable.From(StartParticles)).SetDelay(HitTime + 0.02f);
        // 垫底时长：让 TotalDuration 成为确定的整段时长，预览循环节奏可预期。
        _tween.TweenInterval(TotalDuration);
        _tween.Finished += OnPlaybackFinished;
    }

    private void BuildSlashStage(Tween tween)
    {
        // 主斩与副斩反向交叉扫过，各自带一层拖影。
        BuildSlashArc(tween, _slashMain, SlashMainStart, SlashMainEnd, 0f, false, Vector2.One);
        BuildSlashArc(tween, _slashSub, SlashSubStart, SlashSubEnd, SubSlashDelay, true, Vector2.One);
        BuildSlashArc(tween, _slashMainGhost, SlashMainStart, SlashMainEnd, GhostDelay, false, GhostScaleMul);
        BuildSlashArc(tween, _slashSubGhost, SlashSubStart, SlashSubEnd,
            SubSlashDelay + GhostDelay, true, GhostScaleMul);

        // 斩击层在命中后淡出，让位给爆发层。
        tween.TweenProperty(_slashGroup, "modulate:a", 0f, 0.08f).SetDelay(HitTime + 0.04f);

        tween.TweenCallback(Callable.From(OnHit)).SetDelay(HitTime);
    }

    /// <summary>一条斩击弧的完整扫过：位移越过中心、大幅旋转、由小放大。</summary>
    private void BuildSlashArc(Tween tween, Sprite2D sprite, Vector2 from, Vector2 to,
        float delay, bool flip, Vector2 scaleMul)
    {
        var baseRotation = flip ? _slashSubBaseRotation : _slashMainBaseRotation;
        var baseScale = flip ? _slashSubBaseScale : _slashMainBaseScale;
        var direction = flip ? -1f : 1f;

        var sweep = tween.TweenProperty(sprite, "position", to, SlashDuration);
        sweep.From(from);
        sweep.SetDelay(delay);
        sweep.SetTrans(Tween.TransitionType.Cubic);
        sweep.SetEase(Tween.EaseType.Out);

        var spin = tween.TweenProperty(sprite, "rotation",
            baseRotation + direction * Mathf.DegToRad(SlashOvershootDegrees), SlashDuration);
        spin.From(baseRotation - direction * Mathf.DegToRad(SlashSweepDegrees));
        spin.SetDelay(delay);
        spin.SetTrans(Tween.TransitionType.Cubic);
        spin.SetEase(Tween.EaseType.Out);

        var grow = tween.TweenProperty(sprite, "scale",
            baseScale * SlashEndScaleMul * scaleMul, SlashDuration);
        grow.From(baseScale * SlashStartScaleMul * scaleMul);
        grow.SetDelay(delay);
        grow.SetTrans(Tween.TransitionType.Quad);
        grow.SetEase(Tween.EaseType.Out);
    }

    private void BuildImpactStage(Tween tween)
    {
        // 冲击波：横向压扁的圆环向外扩并淡出。
        var shockScale = tween.TweenProperty(_shockRing, "scale", _shockRingBaseScale * 2.2f, 0.30f);
        shockScale.SetDelay(HitTime);
        shockScale.SetTrans(Tween.TransitionType.Quad);
        shockScale.SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_shockRing, "modulate:a", 0f, 0.30f).SetDelay(HitTime);

        // 扩散环：9 帧序列帧推进，尾段淡出。
        tween.TweenMethod(
            Callable.From<float>(progress => _expandRing.Frame = FrameAt(progress, ExpandRingFrames)),
            0f, 1f, 0.36f).SetDelay(HitTime);
        var expandScale = tween.TweenProperty(_expandRing, "scale", _expandRingBaseScale * 1.6f, 0.36f);
        expandScale.SetDelay(HitTime);
        expandScale.SetTrans(Tween.TransitionType.Quad);
        expandScale.SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_expandRing, "modulate:a", 0f, 0.12f).SetDelay(HitTime + 0.24f);

        // 命中闪光：单帧星芒闪现后急速淡出。
        var coreScale = tween.TweenProperty(_coreFlash, "scale", _coreFlashBaseScale * 1.4f, 0.10f);
        coreScale.From(_coreFlashBaseScale * 0.6f);
        coreScale.SetDelay(HitTime);
        coreScale.SetTrans(Tween.TransitionType.Quad);
        coreScale.SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_coreFlash, "modulate:a", 0f, 0.10f).SetDelay(HitTime);

        // 谱号装饰：命中瞬间亮起并弹入，随后扩散消隐；不做无目的的缓转。
        // 用单一 TweenMethod 同时驱动 alpha 与 scale——并行 Tween 里对同一属性写两次会互相覆盖，
        // 导致「淡入」与「淡出」只有最后一个生效，谱号会一直挂在画面上。
        tween.TweenMethod(
            Callable.From<float>(progress => ApplyCrestState(progress)),
            0f, 1f, CrestLifeTime).SetDelay(HitTime);

        // 暴击叠加层：与闪光同帧闪现，更慢淡出。
        if (IsCritical)
        {
            tween.TweenProperty(_critOverlay, "modulate:a", 0f, 0.28f).SetDelay(HitTime);
        }
    }

    private void OnHit()
    {
        _impactGroup.Visible = true;
    }

    /// <summary>
    /// 谱号装饰的三段式状态：0..1 弹入（由小到大、alpha 拉起到峰值），
    /// 1..2 保持峰值，2..3 轻微扩散并淡出到 0。由单一 TweenMethod 驱动以避免并行争写。
    /// </summary>
    private void ApplyCrestState(float progress)
    {
        var elapsed = progress * CrestLifeTime;

        float alpha;
        float scaleMul;
        if (elapsed < CrestRiseTime)
        {
            // 弹入：Back 缓动让落点略微过冲，读作「盖章」。
            var t = elapsed / CrestRiseTime;
            var eased = BackOut(t);
            alpha = Mathf.Lerp(0f, CrestPeakAlpha, t);
            scaleMul = Mathf.Lerp(0.35f, 1f, eased);
        }
        else if (elapsed < CrestRiseTime + CrestHoldTime)
        {
            alpha = CrestPeakAlpha;
            scaleMul = 1f;
        }
        else
        {
            var t = (elapsed - CrestRiseTime - CrestHoldTime) / CrestFadeTime;
            alpha = Mathf.Lerp(CrestPeakAlpha, 0f, t);
            scaleMul = Mathf.Lerp(1f, 1.12f, t);
        }

        _staffCrest.Modulate = new Color(1f, 1f, 1f, alpha);
        _staffCrest.Scale = _staffCrestBaseScale * scaleMul;
    }

    /// <summary>Back-Out 缓动：起步快、末端轻微过冲后回落。</summary>
    private static float BackOut(float t)
    {
        const float overshoot = 1.70158f;
        var u = t - 1f;
        return u * u * ((overshoot + 1f) * u + overshoot) + 1f;
    }

    private void StartParticles()
    {
        _noteParticles.Restart();
        _starParticles.Restart();
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

    private static void ResetSlashSprite(Sprite2D sprite, Vector2 position,
        Vector2 baseScale, float baseRotation, Color modulate)
    {
        sprite.Position = position;
        sprite.Scale = baseScale;
        sprite.Rotation = baseRotation;
        sprite.Modulate = modulate;
    }

    /// <summary>把归一化进度映射到 0..frameCount-1 的帧号。</summary>
    private static int FrameAt(float progress, int frameCount) =>
        Mathf.Clamp((int)(progress * frameCount), 0, frameCount - 1);
}