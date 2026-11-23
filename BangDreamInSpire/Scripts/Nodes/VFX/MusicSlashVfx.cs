using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamInSpire.Scripts.Nodes.VFX;

/// <summary>音乐斩击与命中特效；仅在 F6 或 Play 时预览。</summary>
[Tool]
public partial class MusicSlashVfx : Node2D
{
    private const float SlashDuration = 0.18f;

    private const float HitTime = 0.14f;

    private const float SubSlashDelay = 0.08f;

    private const float GhostDelay = 0.035f;

    private const float TotalDuration = 0.86f;

    private const float CrestPeakAlpha = 0.85f;

    private const float CrestRiseTime = 0.10f;

    private const float CrestHoldTime = 0.16f;

    private const float CrestFadeTime = 0.22f;

    private const float CrestLifeTime = CrestRiseTime + CrestHoldTime + CrestFadeTime;

    private const int ExpandRingFrames = 9;

    private const float SlashSweepDegrees = 75f;

    private const float SlashOvershootDegrees = 12f;

    private static readonly Vector2 SlashMainStart = new(-86f, -54f);
    private static readonly Vector2 SlashMainEnd = new(18f, 12f);
    private static readonly Vector2 SlashSubStart = new(86f, 54f);
    private static readonly Vector2 SlashSubEnd = new(-18f, -12f);

    private static readonly Vector2 SlashStartScaleMul = new(0.78f, 0.78f);
    private static readonly Vector2 SlashEndScaleMul = new(1.25f, 1.25f);

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
    private Color _slashGroupBaseModulate;
    private Color _slashMainBaseModulate;
    private Color _slashSubBaseModulate;
    private Color _ghostBaseModulate;
    private Color _impactGroupBaseModulate;
    private Color _shockRingBaseModulate;
    private Color _expandRingBaseModulate;
    private Color _coreFlashBaseModulate;
    private Color _staffCrestBaseModulate;
    private Color _critOverlayBaseModulate;

    private Tween? _tween;

    [Export] public Color PreviewTint { get; set; } = new("#7799CC");

    [Export] public bool RandomizeTint { get; set; }

    [Export] public bool IsCritical { get; set; }

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

        // 只读美术基准，编辑器中不改变视觉状态。
        _slashMainBaseScale = _slashMain.Scale;
        _slashSubBaseScale = _slashSub.Scale;
        _shockRingBaseScale = _shockRing.Scale;
        _expandRingBaseScale = _expandRing.Scale;
        _coreFlashBaseScale = _coreFlash.Scale;
        _staffCrestBaseScale = _staffCrest.Scale;
        _slashMainBaseRotation = _slashMain.Rotation;
        _slashSubBaseRotation = _slashSub.Rotation;
        _slashGroupBaseModulate = _slashGroup.Modulate;
        _slashMainBaseModulate = _slashMain.Modulate;
        _slashSubBaseModulate = _slashSub.Modulate;
        _ghostBaseModulate = _slashMainGhost.Modulate;
        _impactGroupBaseModulate = _impactGroup.Modulate;
        _shockRingBaseModulate = _shockRing.Modulate;
        _expandRingBaseModulate = _expandRing.Modulate;
        _coreFlashBaseModulate = _coreFlash.Modulate;
        _staffCrestBaseModulate = _staffCrest.Modulate;
        _critOverlayBaseModulate = _critOverlay.Modulate;

        if (VfxPreviewSupport.AutoPlayOnReady)
            Replay();
    }

    public void Replay()
    {
        _tween?.Kill();

        // 预览时摆到视口中心，游戏内位置由调用方决定。
        VfxPreviewSupport.CenterForPreview(this);

        var tint = RandomizeTint ? VfxPreviewSupport.RandomCharacterColor(this) : PreviewTint;

        // 主题色仅用于斩击层。
        _slashGroup.Visible = true;
        _slashGroup.Modulate = new Color(tint.R, tint.G, tint.B, _slashGroupBaseModulate.A);
        ResetSlashSprite(_slashMain, SlashMainStart, _slashMainBaseScale, _slashMainBaseRotation,
            _slashMainBaseModulate);
        ResetSlashSprite(_slashSub, SlashSubStart, _slashSubBaseScale, _slashSubBaseRotation,
            _slashSubBaseModulate);
        ResetSlashSprite(_slashMainGhost, SlashMainStart, _slashMainBaseScale,
            _slashMainBaseRotation, _ghostBaseModulate);
        ResetSlashSprite(_slashSubGhost, SlashSubStart, _slashSubBaseScale,
            _slashSubBaseRotation, _ghostBaseModulate);

        _impactGroup.Visible = false;
        _impactGroup.Modulate = _impactGroupBaseModulate;
        _shockRing.Scale = _shockRingBaseScale;
        _shockRing.Modulate = _shockRingBaseModulate;
        _expandRing.Frame = 0;
        _expandRing.Scale = _expandRingBaseScale;
        _expandRing.Modulate = _expandRingBaseModulate;
        _coreFlash.Scale = _coreFlashBaseScale;
        _coreFlash.Modulate = _coreFlashBaseModulate;
        _staffCrest.Scale = _staffCrestBaseScale;
        _staffCrest.Modulate = _staffCrestBaseModulate;
        _critOverlay.Visible = IsCritical;
        _critOverlay.Modulate = _critOverlayBaseModulate;

        _noteParticles.Emitting = false;
        _starParticles.Emitting = false;

        _tween = CreateTween();
        _tween.SetParallel(true);
        BuildSlashStage(_tween);
        BuildImpactStage(_tween);
        _tween.TweenCallback(Callable.From(StartParticles)).SetDelay(HitTime + 0.02f);
        // 用垫帧固定总时长。
        _tween.TweenInterval(TotalDuration);
        _tween.Finished += OnPlaybackFinished;
    }

    private void BuildSlashStage(Tween tween)
    {
        // 主副斩反向交叉并带拖影。
        BuildSlashArc(tween, _slashMain, SlashMainStart, SlashMainEnd, 0f, false, Vector2.One);
        BuildSlashArc(tween, _slashSub, SlashSubStart, SlashSubEnd, SubSlashDelay, true, Vector2.One);
        BuildSlashArc(tween, _slashMainGhost, SlashMainStart, SlashMainEnd, GhostDelay, false, GhostScaleMul);
        BuildSlashArc(tween, _slashSubGhost, SlashSubStart, SlashSubEnd,
            SubSlashDelay + GhostDelay, true, GhostScaleMul);

        // 命中后淡出斩击层。
        tween.TweenProperty(_slashGroup, "modulate:a", 0f, 0.08f).SetDelay(HitTime + 0.04f);

        tween.TweenCallback(Callable.From(OnHit)).SetDelay(HitTime);
    }

    /// <summary>驱动单条斩击弧扫过。</summary>
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
        var shockScale = tween.TweenProperty(_shockRing, "scale", _shockRingBaseScale * 2.2f, 0.30f);
        shockScale.SetDelay(HitTime);
        shockScale.SetTrans(Tween.TransitionType.Quad);
        shockScale.SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_shockRing, "modulate:a", 0f, 0.30f).SetDelay(HitTime);

        tween.TweenMethod(
            Callable.From<float>(progress => _expandRing.Frame = FrameAt(progress, ExpandRingFrames)),
            0f, 1f, 0.36f).SetDelay(HitTime);
        var expandScale = tween.TweenProperty(_expandRing, "scale", _expandRingBaseScale * 1.6f, 0.36f);
        expandScale.SetDelay(HitTime);
        expandScale.SetTrans(Tween.TransitionType.Quad);
        expandScale.SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_expandRing, "modulate:a", 0f, 0.12f).SetDelay(HitTime + 0.24f);

        var coreScale = tween.TweenProperty(_coreFlash, "scale", _coreFlashBaseScale * 1.4f, 0.10f);
        coreScale.From(_coreFlashBaseScale * 0.6f);
        coreScale.SetDelay(HitTime);
        coreScale.SetTrans(Tween.TransitionType.Quad);
        coreScale.SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_coreFlash, "modulate:a", 0f, 0.10f).SetDelay(HitTime);

        // 谱号的 alpha 与 scale 由同一 TweenMethod 驱动，避免争写。
        tween.TweenMethod(
            Callable.From<float>(progress => ApplyCrestState(progress)),
            0f, 1f, CrestLifeTime).SetDelay(HitTime);

        if (IsCritical)
        {
            tween.TweenProperty(_critOverlay, "modulate:a", 0f, 0.28f).SetDelay(HitTime);
        }
    }

    private void OnHit()
    {
        _impactGroup.Visible = true;
    }

    /// <summary>驱动谱号弹入、保持与淡出。</summary>
    private void ApplyCrestState(float progress)
    {
        var elapsed = progress * CrestLifeTime;

        float alpha;
        float scaleMul;
        if (elapsed < CrestRiseTime)
        {
            // Back 缓动产生轻微过冲。
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

    /// <summary>Back-Out 缓动。</summary>
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

    /// <summary>将进度映射到帧号。</summary>
    private static int FrameAt(float progress, int frameCount) =>
        Mathf.Clamp((int)(progress * frameCount), 0, frameCount - 1);
}