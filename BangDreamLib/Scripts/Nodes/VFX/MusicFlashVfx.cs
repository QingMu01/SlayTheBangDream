using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace BangDreamLib.Scripts.Nodes.VFX;

public partial class MusicFlashVfx : Node2D
{
    private Node2D? _ringGroup;
    private Sprite2D? _outerRing;
    private Sprite2D? _innerRing;

    private GpuParticles2D? _particles;

    public override void _Ready()
    {
        _ringGroup = GetNode<Node2D>("RingGroup");
        _outerRing = GetNode<Sprite2D>("RingGroup/OuterRing");
        _innerRing = GetNode<Sprite2D>("RingGroup/InnerRing");

        _particles = GetNode<GpuParticles2D>("MusicNoteParticles");

        if (_particles.ProcessMaterial is ParticleProcessMaterial material)
        {
            material.Color = Rng.Chaotic.NextItem(ModelDb.AllCharacters.Select(item => item.NameColor));
        }

        _particles.Emitting = true;

        _ringGroup.Scale = Vector2.Zero;
        var tween = CreateTween();
        tween.TweenInterval(0.3f);
        tween.SetParallel();
        tween.TweenProperty(_ringGroup, "modulate:a", 0, 0.7f);
        tween.TweenProperty(_ringGroup, "scale", new Vector2(1.5f, 1.5f), 0.7f);
        tween.Finished += this.QueueFreeSafely;
    }

    public override void _Process(double delta)
    {
        var rotation = (float)delta * 10f;
        if (_innerRing != null) _innerRing.Rotation += rotation;
        if (_outerRing != null) _outerRing.Rotation -= rotation;
    }
}