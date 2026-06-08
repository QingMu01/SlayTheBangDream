using Godot;
using MegaCrit.Sts2.Core.Random;

namespace BangDreamLib.Scripts.Nodes.VFX;

public partial class MusicWaveVfx : Node2D
{
    private const float Lifetime = 0.8f;
    private const float FloatAmplitude = 12f;
    private const float FadeStartRatio = 1f / 3f;

    private Sprite2D? _musicNote;
    private GpuParticles2D? _particles;

    private TextureRect? _wave;

    private float _noteOriginY;
    private float _elapsed;

    private ShaderMaterial? _waveMaterial;

    public override void _Ready()
    {
        _musicNote = GetNode<Sprite2D>("%MainNote");
        _particles = GetNode<GpuParticles2D>("MusicNoteParticles");
        _wave = GetNode<Node2D>("%Staff").GetNode<TextureRect>("StaffRing");

        _noteOriginY = _musicNote.Position.Y;
        _musicNote.Frame = Rng.Chaotic.NextInt(0, _musicNote.Hframes * _musicNote.Vframes - 1);

        if (_wave.Material is ShaderMaterial material)
        {
            _waveMaterial = material;

            material.SetShaderParameter("speed", 5f);
            material.SetShaderParameter("intensity", 0.15f);

            var hueShift = Rng.Chaotic.NextFloat(0f, 1f);
            var saturation = Rng.Chaotic.NextFloat(0f, 2.0f);
            var brightness = Rng.Chaotic.NextFloat(0.5f, 2.0f);

            material.SetShaderParameter("hue_shift", hueShift);
            material.SetShaderParameter("saturation", saturation);
            material.SetShaderParameter("brightness", brightness);
        }

        _wave.Scale = Vector2.Zero;
    }

    public override void _Process(double delta)
    {
        if (_elapsed == 0f)
        {
            if (_particles != null) _particles.Emitting = true;
        }

        _elapsed += (float)delta;
        var progress = Mathf.Clamp(_elapsed / Lifetime, 0f, 1f);

        if (_musicNote != null)
        {
            var floatOffset = Mathf.Sin(progress * Mathf.Tau) * FloatAmplitude;
            _musicNote.Position = new Vector2(_musicNote.Position.X, _noteOriginY + floatOffset);

            float alpha;
            if (progress <= FadeStartRatio)
            {
                alpha = 1f;
            }
            else
            {
                var fadeProgress = (progress - FadeStartRatio) / (1f - FadeStartRatio);
                alpha = 1f - Mathf.Pow(fadeProgress, 3f);
            }

            var modulate = _musicNote.Modulate;
            modulate.A = alpha;
            _musicNote.Modulate = modulate;
        }

        if (_wave != null)
        {
            var scaleT = 1f - Mathf.Pow(1f - progress, 3f);
            _wave.Scale = Vector2.One * (2.5f * scaleT);
        }

        if (_waveMaterial != null)
        {
            float brightness;
            if (progress <= FadeStartRatio)
            {
                brightness = 1f;
            }
            else
            {
                var fadeProgress = (progress - FadeStartRatio) / (1f - FadeStartRatio);
                brightness = 1f - Mathf.Pow(fadeProgress, 3f);
            }

            _waveMaterial.SetShaderParameter("brightness", brightness);
        }

        if (_elapsed >= Lifetime)
            QueueFree();
    }
}