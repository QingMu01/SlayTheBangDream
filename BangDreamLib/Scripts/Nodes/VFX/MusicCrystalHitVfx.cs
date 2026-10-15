using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamLib.Scripts.Nodes.VFX;

/// <summary>
/// 冷蓝青星芒命中特效：闪光核心 + 爆散星芒 + 底层辉光 + 冲击环 + 飞散火花。
/// 全部素材为 alpha 贴图，用加法混合叠加，不需要压黑处理。
/// 纯表现，基类为 <see cref="Node2D" />（无飞行、无命中结算）。
/// </summary>
[Tool]
public partial class MusicCrystalHitVfx : Node2D
{
    private const float Life = 0.42f;
    private const int SparkCount = 6;

    /// <summary>固定随机种子，保证每次预览的火花方向与距离一致，便于录帧比对。</summary>
    private const int RngSeed = 20261012;

    /// <summary>冷蓝青主色（用户指定）。</summary>
    private static readonly Color Tint = new(0.42f, 0.80f, 1.00f);

    /// <summary>闪光核心提白：二次元风格需要纯白核心，否则整体偏蓝会显脏。</summary>
    private static readonly Color CoreTint = new(0.85f, 0.95f, 1.00f);

    private const float SparkT0 = 0.03f;
    private const float SparkT1 = 0.42f;
    private const float SparkScale0 = 0.16f;
    private const float SparkScale1 = 0.48f;
    private const float SparkAlphaPow = 1.2f;

    /// <summary>层定义，与 plan.md 的层结构表逐行对应。t0/t1 为归一化时间。</summary>
    private sealed record LayerDef(
        string NodePath, Color Tint, float T0, float T1,
        float S0, float S1, float A0, float A1, float Rot, float AlphaPow);

    private static readonly LayerDef[] Layers =
    [
        new("Glow", Tint, 0.00f, 1.00f, 0.30f, 0.95f, 0.55f, 0f, 0f, 1.5f),
        new("Ring", Tint, 0.02f, 0.36f, 0.10f, 1.15f, 0.85f, 0f, 0f, 1.1f),
        // 外层是粗壮星芒，但它剪影带洞（中心为空），必须靠 BurstCore 填掉。
        new("BurstOuter", Tint, 0.00f, 0.34f, 0.18f, 0.95f, 1.00f, 0f, 20f, 0.90f),
        // 内核 z 更高、尺度更小，正好盖住外层的洞；提白以形成"热芯"。
        new("BurstCore", CoreTint, 0.00f, 0.30f, 0.12f, 0.68f, 1.00f, 0f, 12f, 0.85f),
        new("Flash", CoreTint, 0.00f, 0.12f, 0.25f, 0.70f, 1.00f, 0f, 0f, 0.5f),
    ];

    private readonly List<(Sprite2D Node, LayerDef Def, Vector2 BaseScale, float BaseRotation)> _layers = [];

    private readonly List<(Sprite2D Node, Vector2 Offset, Vector2 BaseScale, Vector2 BasePosition, float BaseRotation)> _sparks = [];

    private Tween? _tween;

    /// <summary>勾选即重播一次（自动复位，便于反复触发）。</summary>
    [Export]
    public bool Play
    {
        get => false;
        set { if (value) Replay(); }
    }

    /// <summary>预览态下播放结束后自动重播。</summary>
    [Export] public bool LoopPreview { get; set; } = true;

    public override void _Ready()
    {
        // 铁律一：从场景读回美术基准值，不要硬编码 Vector2.One / Colors.White。
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

                // 均匀分布 + 轻微抖动，避免看起来像机械的六芒星。
                var angle = Mathf.Tau * i / SparkCount + rng.RandfRange(-0.25f, 0.25f);
                var distance = rng.RandfRange(90f, 190f);

                _sparks.Add((spark, Vector2.FromAngle(angle) * distance, spark.Scale, spark.Position, spark.Rotation));
            }
        }

        // _Ready 里不写任何视觉状态；只在运行态播放。
        if (VfxPreviewSupport.AutoPlayOnReady)
            Replay();
    }

    /// <summary>重置并重新播放一次。游戏内由 _Ready 触发，预览可手动/循环触发。</summary>
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

        // 铁律三：单一 TweenMethod 算全部状态，绝不为同一属性建两个 tweener。
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
            // 注意：fade 必须是从 0 走到 1 的插值参数。
            // 若写成 Lerp(A0, A1, 1 - local^pow)，A1=0 时会变成 A0 * local^pow，
            // 曲线方向整个颠倒 —— alpha 反而从 0 涨到 A0，且 local 被 clamp 后永久停在 A0。
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
            // 火花贴图的长轴沿局部 Y 轴。要让它对齐径向 offset.Angle() = φ，
            // 需要旋转 φ - π/2；写成 φ 会让火花切向横躺，看起来像风车的碎片而不是拖尾。
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
