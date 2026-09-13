using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Random;

namespace BangDreamLib.Scripts.Nodes.VFX;

[Tool]
public partial class MusicEqualizerVfx : Node2D
{
    private float _elapsed;

    private FastNoiseLite? _noise;

    private TextureRect? _staffRingVfx;
    private Node2D? _columns;

    private ShaderMaterial? _ringMaterial;

    private Sprite2D[][] _bars = null!;

    private float[] _currentHeight = null!;

    /// <summary>
    /// 每列开始上升的时间（从中心向外延迟，形成波浪感）
    /// </summary>
    private float[] _riseStartTime = null!;

    private bool _active;

    private float _fallSpeed = 3.0f;
    private float _noiseFrequency = 0.3f;
    private int _noiseOctaves = 3;
    private float _riseSpeed = 18.0f;

    [Export] public float Lifetime { get; set; } = 2.5f;

    [Export(PropertyHint.Range, "0.01,2.0")]
    public float NoiseFrequency
    {
        get => _noiseFrequency;
        set
        {
            _noiseFrequency = value;
            ApplyNoise();
        }
    }

    [Export(PropertyHint.Range, "1,6")]
    public int NoiseOctaves
    {
        get => _noiseOctaves;
        set
        {
            _noiseOctaves = value;
            ApplyNoise();
        }
    }

    [Export(PropertyHint.Range, "1.0,30.0")]
    public float RiseSpeed
    {
        get => _riseSpeed;
        set => _riseSpeed = value;
    }

    [Export(PropertyHint.Range, "0.5,15.0")]
    public float FallSpeed
    {
        get => _fallSpeed;
        set => _fallSpeed = value;
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
        _staffRingVfx = GetNode<Node2D>("StaffVfx").GetNode<TextureRect>("StaffRing");
        _columns = GetNode<Node2D>("%Columns");

        if (_staffRingVfx.Material is ShaderMaterial material)
            _ringMaterial = material;

        // 收集所有 Bar 引用。编辑器里保持作者摆放的可见性，便于摆位设计；
        // 真正播放时（Replay）会自行隐藏并在逐帧中按高度显示。
        var columnCount = _columns!.GetChildCount();
        _bars = new Sprite2D[columnCount][];
        var hideBars = !Engine.IsEditorHint();
        for (var i = 0; i < columnCount; i++)
        {
            var column = _columns.GetChild<Node2D>(i);
            var barCount = column.GetChildCount();
            _bars[i] = new Sprite2D[barCount];
            for (var j = 0; j < barCount; j++)
            {
                _bars[i][j] = column.GetChild<Sprite2D>(j);
                if (hideBars)
                {
                    _bars[i][j].Visible = false;
                    _bars[i][j].Scale = Vector2.One;
                }
            }
        }

        _currentHeight = new float[columnCount];
        _riseStartTime = new float[columnCount];

        // 从中心列向外依次延迟，形成波浪式冲起的视觉效果
        var center = (columnCount - 1) / 2.0f;
        for (var i = 0; i < columnCount; i++)
        {
            var distFromCenter = Mathf.Abs(i - center);
            _riseStartTime[i] = distFromCenter * 0.04f;
        }

        _noise = new FastNoiseLite { Seed = Rng.Chaotic.NextInt() };
        ApplyNoise();

        if (VfxPreviewSupport.AutoPlayOnReady)
            Replay();
    }

    /// <summary>重置并重新播放一次特效。游戏内由 _Ready 触发，预览时也可手动/循环触发。</summary>
    public void Replay()
    {
        if (_bars == null)
            return;

        _elapsed = 0f;
        _active = true;

        // 预览时摆到视口中心，游戏内位置由调用方决定。
        VfxPreviewSupport.CenterForPreview(this);

        Array.Clear(_currentHeight, 0, _currentHeight.Length);
        foreach (var column in _bars)
        foreach (var bar in column)
        {
            bar.Visible = false;
            bar.Scale = Vector2.One;
        }

        _columns!.Modulate = Colors.White;
        SetBrightness(1f);
    }

    public override void _Process(double delta)
    {
        if (!_active)
            return;

        var dt = (float)delta;
        _elapsed += dt;

        // ── 均衡器 Bar 增长与回落 ──
        for (var i = 0; i < _bars.Length; i++)
        {
            var barCount = _bars[i].Length;

            // 尚未到达该列的上升起始时间 → 保持隐藏
            if (_elapsed < _riseStartTime[i])
            {
                for (var j = 0; j < barCount; j++)
                    _bars[i][j].Visible = false;
                continue;
            }

            // 每列用不同的噪声偏移，保证列与列之间的差异
            var noiseVal = _noise!.GetNoise2D(_elapsed * 5f, i * 10.0f);
            // noiseVal ∈ [-1, 1] → 归一化到 [0, 1]
            var normalized = Mathf.Clamp((noiseVal + 1f) * 0.5f, 0f, 1f);
            var targetHeight = normalized * barCount;

            // 刚启动时：以 RiseSpeed 快速从底部冲上去
            if (_elapsed - _riseStartTime[i] < 0.2f)
            {
                _currentHeight[i] += RiseSpeed * dt;
                if (_currentHeight[i] > targetHeight)
                    _currentHeight[i] = targetHeight;
            }
            else
            {
                // 正常运行：噪声超过当前高度时瞬间跳到新值（不插值），否则线性回落
                if (targetHeight > _currentHeight[i])
                    _currentHeight[i] = targetHeight;
                else
                    _currentHeight[i] = Mathf.Max(0f, _currentHeight[i] - FallSpeed * barCount * dt);
            }

            var h = _currentHeight[i];
            for (var j = 0; j < barCount; j++)
            {
                var bar = _bars[i][j];

                if (h > j + 1)
                {
                    bar.Visible = true;
                    bar.Scale = Vector2.One;
                }
                else if (h > j)
                {
                    bar.Visible = true;
                    var frac = h - j;
                    bar.Scale = new Vector2(1f, Mathf.Max(0.1f, frac));
                }
                else
                {
                    bar.Visible = false;
                    bar.Scale = Vector2.One;
                }
            }
        }

        // ── 淡出（Lifetime 前最后 1 秒）──
        var remaining = Lifetime - _elapsed;
        if (remaining is < 1.0f and >= 0f)
        {
            var alpha = Mathf.Clamp(remaining / 1.0f, 0f, 1f);
            _columns!.Modulate = new Color(1f, 1f, 1f, alpha);
            SetBrightness(alpha);
        }

        if (_elapsed < Lifetime)
            return;

        _active = false;

        if (VfxPreviewSupport.ShouldSelfFree(this))
        {
            this.QueueFreeSafely();
            return;
        }

        if (LoopPreview)
            Replay();
    }

    /// <summary>
    /// 编辑器里写「实例级」着色参数，避免逐帧改动场景共享的 ShaderMaterial 而把场景标记为已修改；
    /// 游戏内沿用原来写共享材质的写法。
    /// </summary>
    private void SetBrightness(float value)
    {
        if (Engine.IsEditorHint())
            _staffRingVfx?.SetInstanceShaderParameter("brightness", value);
        else
            _ringMaterial?.SetShaderParameter("brightness", value);
    }

    /// <summary>重建噪声参数，使频率/倍频程的改动即时生效。</summary>
    private void ApplyNoise()
    {
        if (_noise == null)
            return;

        _noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _noise.Frequency = _noiseFrequency;
        _noise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _noise.FractalOctaves = _noiseOctaves;
    }
}
