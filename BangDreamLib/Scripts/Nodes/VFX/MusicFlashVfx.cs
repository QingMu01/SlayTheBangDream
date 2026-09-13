using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamLib.Scripts.Nodes.VFX;

[Tool]
public partial class MusicFlashVfx : Node2D
{
    private const float Interval = 0.3f;
    private const float ExpandDuration = 0.7f;
    private const float RotationSpeed = 10f;

    private Sprite2D? _innerRing;
    private Sprite2D? _outerRing;
    private GpuParticles2D? _particles;
    private Node2D? _ringGroup;

    private Tween? _tween;

    /// <summary>是否正在播放。编辑器里未播放时保持静止，避免圆环一直自转干扰设计。</summary>
    private bool _active;

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

    /// <summary>预览态下播放结束后自动重播。</summary>
    [Export] public bool LoopPreview { get; set; } = true;

    public override void _Ready()
    {
        _ringGroup = GetNode<Node2D>("RingGroup");
        _outerRing = GetNode<Sprite2D>("RingGroup/OuterRing");
        _innerRing = GetNode<Sprite2D>("RingGroup/InnerRing");
        _particles = GetNode<GpuParticles2D>("MusicNoteParticles");

        if (VfxPreviewSupport.AutoPlayOnReady)
            Replay();
    }

    /// <summary>重置并重新播放一次特效。游戏内由 _Ready 触发，预览时也可手动/循环触发。</summary>
    public void Replay()
    {
        if (_ringGroup == null || _particles == null)
            return;

        _tween?.Kill();
        _active = true;

        // 预览时摆到视口中心，游戏内位置由调用方决定。
        VfxPreviewSupport.CenterForPreview(this);

        var tint = RandomizeTint
            ? VfxPreviewSupport.RandomCharacterColor(this)
            : PreviewTint;
        ApplyTint(tint);

        _ringGroup.Scale = Vector2.Zero;
        _ringGroup.Modulate = Colors.White;

        var ringGroup = _ringGroup;
        _tween = CreateTween();
        _tween.TweenInterval(Interval);
        _tween.SetParallel();
        _tween.TweenProperty(ringGroup, "modulate:a", 0f, ExpandDuration);
        _tween.TweenProperty(ringGroup, "scale", new Vector2(1.5f, 1.5f), ExpandDuration);
        _tween.Finished += OnPlaybackFinished;

        _particles.Restart();
    }

    public override void _Process(double delta)
    {
        if (!_active)
            return;

        var rotation = (float)delta * RotationSpeed;
        if (_innerRing != null) _innerRing.Rotation += rotation;
        if (_outerRing != null) _outerRing.Rotation -= rotation;
    }

    /// <summary>
    /// 游戏内沿用原本的粒子材质着色；预览时改用节点自身着色，
    /// 避免在编辑器中写坏场景内共享的 process_material 子资源。
    /// </summary>
    private void ApplyTint(Color tint)
    {
        if (_particles == null)
            return;

        if (Engine.IsEditorHint())
        {
            _particles.SelfModulate = tint;
            return;
        }

        if (_particles.ProcessMaterial is ParticleProcessMaterial material)
            material.Color = tint;
    }

    private void OnPlaybackFinished()
    {
        if (VfxPreviewSupport.ShouldSelfFree(this))
        {
            _active = false;
            this.QueueFreeSafely();
            return;
        }

        if (LoopPreview)
        {
            Replay();
            return;
        }

        _active = false;
    }
}
