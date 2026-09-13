using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Random;

namespace BangDreamLib.Scripts.Nodes.VFX;

[Tool]
public partial class MusicWaveVfx : Node2D
{
    private const float Lifetime = 0.8f;
    private const float FloatAmplitude = 12f;
    private const float FadeStartRatio = 1f / 3f;
    private const string ParamSpeed = "speed";
    private const string ParamIntensity = "intensity";
    private const string ParamHueShift = "hue_shift";
    private const string ParamSaturation = "saturation";
    private const string ParamBrightness = "brightness";

    private bool _active;
    private float _elapsed;

    /// <summary>场景中 MainNote 的原始着色（含蓝色基调）。原实现只动画其 alpha，这里保持一致。</summary>
    private Color _noteBaseColor = Colors.White;
    private float _hueShift;
    private Sprite2D? _musicNote;

    private float _noteOriginY;
    private GpuParticles2D? _particles;
    private float _saturation = 1f;
    private float _waveIntensity = 0.15f;
    private TextureRect? _wave;

    private ShaderMaterial? _waveMaterial;
    private float _waveSpeed = 5f;

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

    /// <summary>每次播放时随机化色相/饱和度（预览调参时建议关闭）。</summary>
    [Export] public bool RandomizeColor { get; set; } = true;

    [Export(PropertyHint.Range, "0,20,0.1")]
    public float WaveSpeed
    {
        get => _waveSpeed;
        set
        {
            _waveSpeed = value;
            ApplyShaderParam(ParamSpeed, value);
        }
    }

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float WaveIntensity
    {
        get => _waveIntensity;
        set
        {
            _waveIntensity = value;
            ApplyShaderParam(ParamIntensity, value);
        }
    }

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float HueShift
    {
        get => _hueShift;
        set
        {
            _hueShift = value;
            ApplyShaderParam(ParamHueShift, value);
        }
    }

    [Export(PropertyHint.Range, "0,2,0.01")]
    public float Saturation
    {
        get => _saturation;
        set
        {
            _saturation = value;
            ApplyShaderParam(ParamSaturation, value);
        }
    }

    public override void _Ready()
    {
        _musicNote = GetNode<Sprite2D>("%MainNote");
        _particles = GetNode<GpuParticles2D>("MusicNoteParticles");
        _wave = GetNode<Node2D>("%Staff").GetNode<TextureRect>("StaffRing");

        _noteOriginY = _musicNote.Position.Y;
        _noteBaseColor = _musicNote.Modulate;

        if (_wave.Material is ShaderMaterial material)
            _waveMaterial = material;

        if (VfxPreviewSupport.AutoPlayOnReady)
            Replay();
    }

    /// <summary>重置并重新播放一次特效。游戏内由 _Ready 触发，预览时也可手动/循环触发。</summary>
    public void Replay()
    {
        if (_musicNote == null || _wave == null)
            return;

        _elapsed = 0f;
        _active = true;

        // 预览时摆到视口中心，游戏内位置由调用方决定。
        VfxPreviewSupport.CenterForPreview(this);

        _musicNote.Frame = Rng.Chaotic.NextInt(0, _musicNote.Hframes * _musicNote.Vframes - 1);
        _musicNote.Modulate = _noteBaseColor;
        _musicNote.Position = new Vector2(_musicNote.Position.X, _noteOriginY);
        _wave.Scale = Vector2.Zero;

        if (RandomizeColor)
        {
            HueShift = Rng.Chaotic.NextFloat(0f, 1f);
            Saturation = Rng.Chaotic.NextFloat(0f, 2.0f);
        }

        ApplyShaderParams();
        _particles?.Restart();
    }

    public override void _Process(double delta)
    {
        if (!_active)
            return;

        _elapsed += (float)delta;
        var progress = Mathf.Clamp(_elapsed / Lifetime, 0f, 1f);

        if (_musicNote != null)
        {
            var floatOffset = Mathf.Sin(progress * Mathf.Tau) * FloatAmplitude;
            _musicNote.Position = new Vector2(_musicNote.Position.X, _noteOriginY + floatOffset);

            var modulate = _musicNote.Modulate;
            modulate.A = FadeCurve(progress);
            _musicNote.Modulate = modulate;
        }

        if (_wave != null)
        {
            var scaleT = 1f - Mathf.Pow(1f - progress, 3f);
            _wave.Scale = Vector2.One * (2.5f * scaleT);
        }

        if (_waveMaterial != null || Engine.IsEditorHint())
            SetBrightness(FadeCurve(progress));

        if (_elapsed < Lifetime)
            return;

        _active = false;

        if (VfxPreviewSupport.ShouldSelfFree(this))
        {
            QueueFree();
            return;
        }

        if (LoopPreview)
            Replay();
    }

    private void ApplyShaderParams()
    {
        ApplyShaderParam(ParamSpeed, WaveSpeed);
        ApplyShaderParam(ParamIntensity, WaveIntensity);
        ApplyShaderParam(ParamHueShift, HueShift);
        ApplyShaderParam(ParamSaturation, Saturation);
    }

    /// <summary>
    /// 编辑器里写「实例级」着色参数：它是每实例的，不会改动场景共享的 ShaderMaterial，
    /// 因而不会把场景标记为已修改（逐帧的 brightness 尤其如此）。游戏内沿用原来的写法。
    /// </summary>
    private void ApplyShaderParam(StringName name, Variant value)
    {
        if (Engine.IsEditorHint())
            _wave?.SetInstanceShaderParameter(name, value);
        else
            _waveMaterial?.SetShaderParameter(name, value);
    }

    /// <summary>编辑器写实例级参数，游戏内写共享材质参数（见 ApplyShaderParam 的说明）。</summary>
    private void SetBrightness(float value)
    {
        ApplyShaderParam(ParamBrightness, value);
    }

    /// <summary>先恒定再三次衰减的淡出曲线，与原始视觉一致。</summary>
    private static float FadeCurve(float progress)
    {
        if (progress <= FadeStartRatio)
            return 1f;

        var fadeProgress = (progress - FadeStartRatio) / (1f - FadeStartRatio);
        return 1f - Mathf.Pow(fadeProgress, 3f);
    }
}
