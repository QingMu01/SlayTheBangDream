using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamLib.Scripts.Nodes.VFX;

[Tool]
public partial class MusicHitVfx : Node2D
{
    private const float Duration = 1.2f;

    private GpuParticles2D? _particles;
    private Node2D? _ring;

    private Tween? _tween;

    /// <summary>预览时使用的固定着色；<see cref="RandomizeTint" /> 为 true 时忽略。</summary>
    [Export]
    public Color PreviewTint { get; set; } = new("#FF5555");

    [Export] public bool RandomizeTint { get; set; } = true;

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
        _ring = GetNode<Node2D>("RingGroup");
        _particles = GetNode<GpuParticles2D>("StarParticles");

        if (VfxPreviewSupport.AutoPlayOnReady)
            Replay();
    }

    /// <summary>重置并重新播放一次特效。游戏内由 _Ready 触发，预览时也可手动/循环触发。</summary>
    public void Replay()
    {
        if (_ring == null || _particles == null)
            return;

        _tween?.Kill();

        // 预览时摆到视口中心，游戏内位置由调用方决定。
        VfxPreviewSupport.CenterForPreview(this);

        _ring.Modulate = RandomizeTint
            ? VfxPreviewSupport.RandomCharacterColor(this)
            : PreviewTint;
        _ring.Scale = Vector2.Zero;

        var ring = _ring;
        _tween = CreateTween();
        _tween.SetParallel();
        _tween.TweenProperty(ring, "scale", new Vector2(1f, 0.5f), Duration);
        _tween.TweenProperty(ring, "modulate:a", 0f, Duration);
        _tween.Finished += OnPlaybackFinished;

        _particles.Restart();
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
}
