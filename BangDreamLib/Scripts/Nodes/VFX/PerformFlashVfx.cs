using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamLib.Scripts.Nodes.VFX;

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

    private Color _flashColor = new("#d30150");

    public Color FlashColor
    {
        get => _flashColor;
        set
        {
            _flashColor = value;
            ApplyFlashColor();
        }
    }

    public override void _Ready()
    {
        _sweepLight = GetNode<ColorRect>("SweepLight");
        _noteParticles = GetNode<GpuParticles2D>("MusicNoteParticles");
        _starParticles = GetNode<GpuParticles2D>("StarParticles");

        ApplyFlashColor();
        SetSweepProgress(0f);

        if (_noteParticles != null) _noteParticles.Emitting = true;

        var tween = CreateTween();
        tween.SetPauseMode(Tween.TweenPauseMode.Process);
        tween.SetParallel();

        tween.TweenMethod(
                Callable.From<double>(progress => SetSweepProgress((float)progress)),
                0d,
                1d,
                SweepDuration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenCallback(Callable.From(() =>
            {
                if (_starParticles != null) _starParticles.Emitting = true;
            }))
            .SetDelay(StarEmissionDelay);

        tween.Chain().TweenInterval(CleanupDelay);
        tween.Finished += this.QueueFreeSafely;
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

        if (_noteParticles?.ProcessMaterial is ParticleProcessMaterial noteMaterial)
        {
            noteMaterial.Color = _flashColor.Lightened(0.2f);
        }

        if (_starParticles?.ProcessMaterial is ParticleProcessMaterial starMaterial)
        {
            starMaterial.Color = _flashColor.Lightened(0.55f);
        }
    }
}