using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace BangDreamLib.Scripts.Nodes.VFX;

public partial class MusicHitVfx : Node2D
{
    private Node2D? _ring;

    private GpuParticles2D? _particles;

    public override void _Ready()
    {
        _ring = GetNode<Node2D>("RingGroup");
        _particles = GetNode<GpuParticles2D>("StarParticles");

        _ring.Scale = Vector2.Zero;
        var tween = CreateTween();
        tween.SetParallel();
        tween.TweenProperty(_ring, "scale", new Vector2(1f, 0.5f), 1.2f);
        tween.TweenProperty(_ring, "modulate:a", 0f, 1.2f);
        tween.Finished += this.QueueFreeSafely;

        _ring.Modulate = Rng.Chaotic.NextItem(ModelDb.AllCharacters.Select(item => item.NameColor));
        _particles.Emitting = true;
    }
}