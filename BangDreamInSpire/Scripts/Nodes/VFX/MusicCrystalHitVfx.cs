using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamInSpire.Scripts.Nodes.VFX;

/// <summary>冷蓝星芒命中特效，纯表现。</summary>
[Tool]
public partial class MusicCrystalHitVfx : Node2D
{
    private const float Life = 0.42f;
    private const int SparkCount = 6;

    /// <summary>固定预览随机种子。</summary>
    private const int RngSeed = 20261012;

    private static readonly Color Tint = new(0.42f, 0.80f, 1.00f);

    private static readonly Color CoreTint = new(0.85f, 0.95f, 1.00f);

    private const float SparkT0 = 0.03f;
    private const float SparkT1 = 0.42f;
    private const float SparkScale0 = 0.16f;
    private const float SparkScale1 = 0.48f;
    private const float SparkAlphaPow = 1.2f;

    /// <summary>表现层参数。</summary>
    private sealed record LayerDef(
        string NodePath, Color Tint, float T0, float T1,
        float S0, float S1, float A0, float A1, float Rot, float AlphaPow);

    private static readonly LayerDef[] Layers =
    [
        new("Glow", Tint, 0.00f, 1.00f, 0.30f, 0.95f, 0.55f, 0f, 0f, 1.5f),
        new("Ring", Tint, 0.02f, 0.36f, 0.10f, 1.15f, 0.85f, 0f, 0f, 1.1f),
        // BurstCore 填充外层星芒的中心空洞。
        new("BurstOuter", Tint, 0.00f, 0.34f, 0.18f, 0.95f, 1.00f, 0f, 20f, 0.90f),
        new("BurstCore", CoreTint, 0.00f, 0.30f, 0.12f, 0.68f, 1.00f, 0f, 12f, 0.85f),
        new("Flash", CoreTint, 0.00f, 0.12f, 0.25f, 0.70f, 1.00f, 0f, 0f, 0.5f),
    ];

    private readonly List<(Sprite2D Node, LayerDef Def, Vector2 BaseScale, float BaseRotation)> _layers = [];

    private readonly List<(Sprite2D Node, Vector2 Offset, Vector2 BaseScale, Vector2 BasePosition, float BaseRotation)> _sparks = [];

    private Tween? _tween;

    [Export]
    public bool Play
    {
        get => false;
        set { if (value) Replay(); }
    }

    [Export] public bool LoopPreview { get; set; } = true;

    public override void _Ready()
    {
        // 从场景读取美术基准。
        foreach (var def in Layers)
        {
            if (GetNodeOrNull<Sprite2D>(def.NodePath) is not { } sprite)
                continue;

            _layers.Add((sprite, def, sprite.Scale, sprite.Rotation));
        }

        if (GetNodeOrNull<Node2D>("Sparks") is { } sparks)
        {
            var rng = new RandomNumberGenerator { Seed = RngSeed };

            for (var i = 0; i < SparkCount; i++)
            {
                if (sparks.GetNodeOrNull<Sprite2D>($"Spark{i}") is not { } spark)
                    continue;

                // 均匀分布并加入轻微抖动。
                var angle = Mathf.Tau * i / SparkCount + rng.RandfRange(-0.25f, 0.25f);
                var distance = rng.RandfRange(90f, 190f);

                _sparks.Add((spark, Vector2.FromAngle(angle) * distance, spark.Scale, spark.Position, spark.Rotation));
            }
        }

        // 仅运行态播放。
        if (VfxPreviewSupport.AutoPlayOnReady)
            Replay();
    }

    public void Replay()
    {
        _tween?.Kill();

        VfxPreviewSupport.CenterForPreview(this);

        foreach (var (node, _, baseScale, baseRotation) in _layers)
        {
            node.Scale = baseScale;
            node.Rotation = baseRotation;
            node.Modulate = Colors.White;
        }

        foreach (var (node, _, baseScale, basePosition, baseRotation) in _sparks)
        {
            node.Position = basePosition;
            node.Scale = baseScale;
            node.Rotation = baseRotation;
            node.Modulate = Colors.White;
        }

        // 所有状态由单一 TweenMethod 驱动。
        _tween = CreateTween();
        _tween.TweenMethod(Callable.From<float>(ApplyState), 0f, 1f, Life);
        _tween.Finished += OnPlaybackFinished;
    }

    private void ApplyState(float progress)
    {
        foreach (var (node, def, baseScale, baseRotation) in _layers)
        {
            var span = Mathf.Max(def.T1 - def.T0, 0.0001f);
            var local = Mathf.Clamp((progress - def.T0) / span, 0f, 1f);

            var eScale = 1f - Mathf.Pow(1f - local, 3f);          // 缓出
            // fade 必须从 0 递增到 1。
            var fade = Mathf.Pow(local, def.AlphaPow);            // pow 越小消失越快

            node.Scale = baseScale * Mathf.Lerp(def.S0, def.S1, eScale);
            node.Rotation = baseRotation + Mathf.DegToRad(def.Rot * eScale);
            node.Modulate = new Color(def.Tint.R, def.Tint.G, def.Tint.B,
                Mathf.Lerp(def.A0, def.A1, fade));
        }

        var sparkSpan = Mathf.Max(SparkT1 - SparkT0, 0.0001f);
        var sparkLocal = Mathf.Clamp((progress - SparkT0) / sparkSpan, 0f, 1f);
        var sparkScale = Mathf.Lerp(SparkScale0, SparkScale1, 1f - Mathf.Pow(1f - sparkLocal, 3f));
        var sparkFade = Mathf.Pow(sparkLocal, SparkAlphaPow);

        foreach (var (node, offset, baseScale, basePosition, baseRotation) in _sparks)
        {
            node.Position = basePosition + offset * sparkLocal;
            node.Scale = baseScale * sparkScale;
            // 火花长轴沿局部 Y，径向对齐需旋转 φ - π/2。
            node.Rotation = baseRotation + offset.Angle() - Mathf.Pi * 0.5f;
            node.Modulate = new Color(Tint.R, Tint.G, Tint.B, Mathf.Lerp(1f, 0f, sparkFade));
        }
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
