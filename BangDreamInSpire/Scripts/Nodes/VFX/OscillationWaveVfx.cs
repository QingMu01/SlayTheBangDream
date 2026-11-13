using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace BangDreamInSpire.Scripts.Nodes.VFX;

/// <summary>
/// 祥子「震荡」的施法者层：以施法者为中心扩散的**双层五线谱声波环** + 地面辉光，
/// 收尾时向内收缩的余音环与光脉冲，把"声波推出去 → 音收回来"写成一条时间线。
/// 纯表现，无飞行、无结算；游戏内由 <c>WithAttackerFx</c> 挂到施法者身上。
/// </summary>
[Tool]
public partial class OscillationWaveVfx : Node2D
{
    private const string ScenePath = "res://BangDreamInSpire/scenes/vfx/oscillation_wave_vfx.tscn";

    /// <summary>整段时长：两圈外扩波（0–0.62）+ 收尾的余音收缩环（0.34–0.62）。</summary>
    private const float Life = 0.62f;

    /// <summary>层定义，与 plan.md 的层结构表逐行对应。t0/t1 为归一化时间。</summary>
    private sealed record LayerDef(
        string NodePath, Color Tint, float T0, float T1,
        float S0, float S1, float A0, float A1, float Rot, float AlphaPow);

    private static readonly LayerDef[] Layers =
    [
        // 地面辉光：把整段演出"压"在地上，只在开场存在。
        new("GroundGlow", new Color(0.62f, 0.72f, 0.95f), 0.00f, 0.75f, 1.20f, 2.20f, 0.30f, 0f, 0f, 1.80f),
        // 谱面：被拨响的五线谱环从施法者身上炸开，随后让位给声波环。
        new("Score", new Color(0.72f, 0.80f, 1.00f), 0.00f, 0.50f, 0.35f, 1.20f, 0.90f, 0f, 18f, 1.60f),
        // 声波 A：软环外扩，第一声。
        new("WaveA", new Color(0.70f, 0.78f, 1.00f), 0.04f, 0.85f, 0.55f, 2.10f, 0.85f, 0f, -10f, 1.90f),
        // 声波 B：锐环更外一圈，给外缘一道硬锋。
        new("WaveB", new Color(0.66f, 0.76f, 1.00f), 0.10f, 0.95f, 0.90f, 2.45f, 0.70f, 0f, 0f, 1.40f),
        // 余音回收：收尾时一圈光从外向内收，和命中层音符飞回的时刻对齐。
        new("Echo", new Color(0.72f, 0.80f, 1.00f), 0.62f, 0.96f, 1.00f, 0.42f, 0.60f, 0f, 12f, 2.00f),
    ];

    private readonly List<(Sprite2D Node, LayerDef Def, Vector2 BaseScale, float BaseRotation)> _layers = [];

    private Tween? _tween;

    /// <summary>游戏内落点（施法者 <c>VfxSpawnPosition</c> 的世界坐标），<c>_Ready</c> 里应用。</summary>
    private Vector2 _spawnWorld;

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

    /// <summary>游戏内创建施法者层特效；定位与本体 <c>VfxCmd.PlayOnCreatureCenter</c> 相同。</summary>
    public static OscillationWaveVfx? Create(Creature caster)
    {
        if (TestMode.IsOn)
            return null;

        if (NCombatRoom.Instance?.GetCreatureNode(caster) is not { } creatureNode)
            return null;

        var vfx = BangDreamPreloadManager.GetScene(ScenePath).Instantiate<OscillationWaveVfx>();
        vfx._spawnWorld = creatureNode.VfxSpawnPosition;
        return vfx;
    }

    public override void _Ready()
    {
        // 铁律一：从场景读回美术基准值，不硬编码 Vector2.One / Colors.White。
        foreach (var def in Layers)
        {
            if (GetNodeOrNull<Sprite2D>(def.NodePath) is not { } sprite)
                continue;

            _layers.Add((sprite, def, sprite.Scale, sprite.Rotation));
        }

        // 先挂进树再定位（与 VfxCmd.PlayVfx 同序），避免依赖容器的变换是否为恒等。
        if (_spawnWorld != Vector2.Zero)
            GlobalPosition = _spawnWorld;

        // _Ready 里不写任何视觉状态；只在运行态播放。
        if (VfxPreviewSupport.AutoPlayOnReady)
            Replay();
    }

    /// <summary>重置并重新播放一次。</summary>
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

        // 铁律三：单一 TweenMethod 计算全部状态，不为同一属性建两个 tweener。
        _tween = CreateTween();
        _tween.TweenMethod(Callable.From<float>(ApplyState), 0f, 1f, Life);
        _tween.Finished += OnPlaybackFinished;
    }

    private void ApplyState(float progress)
    {
        var clamped = Mathf.Clamp(progress, 0f, 1f);

        foreach (var (node, def, baseScale, baseRotation) in _layers)
        {
            var span = Mathf.Max(def.T1 - def.T0, 0.0001f);
            var local = Mathf.Clamp((clamped - def.T0) / span, 0f, 1f);

            // 层还没到登场时间：必须完全不显示，否则会在原地留一块静态贴图。
            if (clamped < def.T0)
            {
                node.Scale = baseScale * def.S0;
                node.Modulate = new Color(def.Tint.R, def.Tint.G, def.Tint.B, 0f);
                continue;
            }

            var eScale = EaseOutCubic(local);
            // 注意：fade 必须是从 0 走到 1 的插值参数。
            // 若写成 Lerp(A0, A1, 1 - local^pow)，A1=0 时会变成 A0 * local^pow，
            // 曲线方向整个颠倒 —— alpha 反而从 0 涨到 A0，且 local 被 clamp 后永久停在 A0。
            var fade = Mathf.Pow(local, def.AlphaPow);

            node.Scale = baseScale * Mathf.Lerp(def.S0, def.S1, eScale);
            node.Rotation = baseRotation + Mathf.DegToRad(def.Rot * eScale);
            node.Modulate = new Color(def.Tint.R, def.Tint.G, def.Tint.B,
                Mathf.Lerp(def.A0, def.A1, fade));
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

    private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
}
