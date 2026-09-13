using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamLib.Scripts.Nodes.VFX;

[Tool]
public partial class PerformFlashVfx : Node2D
{
    private static readonly StringName RevealProgressShaderParameter = "reveal_progress";

    private const float SweepDuration = 0.45f;
    private const float StarEmissionDelay = 0.03f;
    private const float CleanupDelay = 0.55f;
    private const float SweepAlpha = 0.7f;

    private ColorRect? _sweepLight;
    private GpuParticles2D? _noteParticles;
    private GpuParticles2D? _starParticles;

    private Tween? _tween;

    private Color _flashColor = new("#d30150");

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
    [Export] public bool LoopPreview { get; set; } = true;

    public override void _Ready()
    {
        _sweepLight = GetNode<ColorRect>("SweepLight");
        _noteParticles = GetNode<GpuParticles2D>("MusicNoteParticles");
        _starParticles = GetNode<GpuParticles2D>("StarParticles");

        // 编辑器里不主动写着色，避免打开场景就把默认值写回、把场景标记为已修改；
        // 设计师改动 FlashColor 时由 setter 生效。
        if (!Engine.IsEditorHint())
            ApplyFlashColor();

        if (VfxPreviewSupport.AutoPlayOnReady)
            Replay();
    }

    /// <summary>重置并重新播放一次特效。游戏内由 _Ready 触发，预览时也可手动/循环触发。</summary>
    public void Replay()
    {
        if (_sweepLight == null)
            return;

        _tween?.Kill();

        // 预览时摆到视口中心，游戏内位置由调用方（演奏区）决定。
        VfxPreviewSupport.CenterForPreview(this);
        SetSweepProgress(0f);

        _tween = CreateTween();
        _tween.SetPauseMode(Tween.TweenPauseMode.Process);
        _tween.SetParallel();

        _tween.TweenMethod(
                Callable.From<double>(progress => SetSweepProgress((float)progress)),
                0d,
                1d,
                SweepDuration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        _tween.TweenCallback(Callable.From(() =>
            {
                if (_starParticles != null) _starParticles.Emitting = true;
            }))
            .SetDelay(StarEmissionDelay);

        _tween.Chain().TweenInterval(CleanupDelay);
        _tween.Finished += OnPlaybackFinished;

        _noteParticles?.Restart();
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
        _sweepLight?.SetInstanceShaderParameter(RevealProgressShaderParameter, Mathf.Clamp(progress, 0f, 1f));
    }

    private void ApplyFlashColor()
    {
        if (_sweepLight != null)
        {
            var sweepColor = _flashColor.Lightened(0.35f);
            sweepColor.A = SweepAlpha;
            _sweepLight.Color = sweepColor;
        }

        // 编辑器里粒子材质是场景内的共享子资源，直接改会污染场景；预览改用节点自身着色。
        var useSelfModulate = Engine.IsEditorHint();

        if (_noteParticles != null)
        {
            var noteColor = _flashColor.Lightened(0.2f);
            if (useSelfModulate)
                _noteParticles.SelfModulate = noteColor;
            else if (_noteParticles.ProcessMaterial is ParticleProcessMaterial noteMaterial)
                noteMaterial.Color = noteColor;
        }

        if (_starParticles != null)
        {
            var starColor = _flashColor.Lightened(0.55f);
            if (useSelfModulate)
                _starParticles.SelfModulate = starColor;
            else if (_starParticles.ProcessMaterial is ParticleProcessMaterial starMaterial)
                starMaterial.Color = starColor;
        }
    }
}
