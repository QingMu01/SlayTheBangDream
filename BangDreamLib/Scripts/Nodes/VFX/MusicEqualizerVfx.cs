using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Random;

namespace BangDreamLib.Scripts.Nodes.VFX;

public partial class MusicEqualizerVfx : Node2D
{
    [Export] public float Lifetime { get; set; } = 2.5f;

    [Export(PropertyHint.Range, "0.01,2.0")]
    public float NoiseFrequency { get; set; } = 0.3f;

    [Export(PropertyHint.Range, "1,6")] public int NoiseOctaves { get; set; } = 3;

    [Export(PropertyHint.Range, "1.0,30.0")]
    public float RiseSpeed { get; set; } = 18.0f;

    [Export(PropertyHint.Range, "0.5,15.0")]
    public float FallSpeed { get; set; } = 3.0f;

    private float _elapsed;

    private TextureRect? _staffRingVfx;
    private Node2D? _columns;

    private FastNoiseLite? _noise;
    private ShaderMaterial? _ringMaterial;

    private Sprite2D[][] _bars = null!;

    private float[] _currentHeight = null!;

    /// <summary>
    /// 每列开始上升的时间（从中心向外延迟，形成波浪感）
    /// </summary>
    private float[] _riseStartTime = null!;

    public override void _Ready()
    {
        _staffRingVfx = GetNode<Node2D>("StaffVfx").GetNode<TextureRect>("StaffRing");
        _columns = GetNode<Node2D>("%Columns");

        _noise = new FastNoiseLite
        {
            Seed = Rng.Chaotic.NextInt(),
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
            Frequency = NoiseFrequency,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = NoiseOctaves,
        };

        if (_staffRingVfx.Material is ShaderMaterial material)
        {
            _ringMaterial = material;

            material.SetShaderParameter("speed", 5f);
            material.SetShaderParameter("intensity", 0.15f);
        }

        // 收集所有 Bar 引用并初始化为不可见
        var columnCount = _columns!.GetChildCount();
        _bars = new Sprite2D[columnCount][];
        for (var i = 0; i < columnCount; i++)
        {
            var column = _columns.GetChild<Node2D>(i);
            var barCount = column.GetChildCount();
            _bars[i] = new Sprite2D[barCount];
            for (var j = 0; j < barCount; j++)
            {
                _bars[i][j] = column.GetChild<Sprite2D>(j);
                _bars[i][j].Visible = false;
                _bars[i][j].Scale = Vector2.One;
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
    }

    public override void _Process(double delta)
    {
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
            _ringMaterial?.SetShaderParameter("brightness", alpha);
        }

        if (_elapsed >= Lifetime)
            this.QueueFreeSafely();
    }
}