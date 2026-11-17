using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace BangDreamInSpire.Scripts.Nodes.VFX;

/// <summary>
/// 祥子「共鸣」的打出特效：乐谱符号收拢压缩 → 闪光 → 双音符环同向扩出、符号被放大弹射。
/// 纯表现，无飞行、无结算；游戏内由 <c>Resonance.OnPlay</c> 挂到施法者 VfxContainer。
/// </summary>
[Tool]
public partial class ResonanceVfx : Node2D
{
    private const string ScenePath = "res://BangDreamInSpire/scenes/vfx/resonance_vfx.tscn";

    /// <summary>整段时长：收拢 0–0.30 + 压缩闪光 0.27–0.49 + 双环 0.29–0.87 + 弹射 0.34–0.80 + 收尾。</summary>
    private const float Life = 0.95f;

    private const float GatherEnd = 0.30f;
    private const float HoldEnd = 0.34f;
    private const float EjectEnd = 0.798f;
    private const float RadiusOut = 190f;
    private const float RadiusIn = 46f;
    private const float RadiusEject = 215f;

    private enum LayerKind { Glow, Ring, Note, Glyph, Flash }

    private sealed record LayerDef(
        string NodePath, LayerKind Kind, Color Tint, Color TintOut,
        float T0, float T1, float S0, float SMid, float S1,
        float A0, float A1, float Rot, float AlphaPow, float Theta);

    private static readonly LayerDef[] Layers =
    [
        new("GroundGlow", LayerKind.Glow, new Color(0.49f, 0.55f, 1.00f), default,
            0.00f, 0.90f, 0.50f, 0f, 1.00f, 0.75f, 0f, 0f, 1.60f, 0f),
        new("RingInner", LayerKind.Ring, new Color(0.54f, 0.61f, 1.00f), default,
            0.30f, 0.68f, 0.42f, 0f, 0.66f, 0.90f, 0f, -8f, 1.70f, 0f),
        new("RingOuter", LayerKind.Ring, new Color(0.54f, 0.61f, 1.00f), default,
            0.44f, 0.92f, 0.55f, 0f, 0.95f, 0.85f, 0f, 10f, 1.50f, 0f),
        new("NoteCore", LayerKind.Note, new Color(0.86f, 0.89f, 1.00f), default,
            0.04f, 1.00f, 0.14f, 0f, 0.39f, 1.00f, 0f, 0f, 2.00f, 0f),
        new("Glyph0", LayerKind.Glyph, new Color(0.56f, 0.62f, 1.00f), new Color(0.73f, 0.78f, 1.00f),
            0.00f, 0.84f, 0.1875f, 0.1354f, 0.2396f, 0.85f, 0f, 0f, 1.40f, 45f),
        new("Glyph1", LayerKind.Glyph, new Color(0.56f, 0.62f, 1.00f), new Color(0.73f, 0.78f, 1.00f),
            0.00f, 0.84f, 0.2135f, 0.1510f, 0.2396f, 0.85f, 0f, 0f, 1.40f, 135f),
        new("Glyph2", LayerKind.Glyph, new Color(0.56f, 0.62f, 1.00f), new Color(0.73f, 0.78f, 1.00f),
            0.00f, 0.84f, 0.1979f, 0.1406f, 0.2188f, 0.85f, 0f, 0f, 1.40f, 225f),
        new("Glyph3", LayerKind.Glyph, new Color(0.56f, 0.62f, 1.00f), new Color(0.73f, 0.78f, 1.00f),
            0.00f, 0.84f, 0.1563f, 0.1198f, 0.1719f, 0.85f, 0f, 0f, 1.40f, 315f),
        new("Flash", LayerKind.Flash, new Color(0.84f, 0.86f, 1.00f), default,
            0.28f, 0.52f, 0.10f, 0f, 0.30f, 1.00f, 0f, 0f, 1.80f, 0f),
    ];

    private readonly List<(Sprite2D Node, LayerDef Def, Vector2 BaseScale, float BaseRotation)> _layers = [];

    private Tween? _tween;

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

    /// <summary>游戏内创建打出特效；定位与本体 <c>VfxCmd.PlayOnCreatureCenter</c> 相同。</summary>
    public static ResonanceVfx? Create(Creature caster)
    {
        if (TestMode.IsOn)
            return null;

        if (NCombatRoom.Instance?.GetCreatureNode(caster) is not { } creatureNode)
            return null;

        var vfx = BangDreamPreloadManager.GetScene(ScenePath).Instantiate<ResonanceVfx>();
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
            node.Position = Vector2.Zero;
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
        var seconds = clamped * Life;

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

            var e = EaseOutCubic(local);
            // fade 必须是从 0 走到 1 的插值参数，写反会让 alpha 永久停在峰值。
            var fade = Mathf.Pow(local, def.AlphaPow);
            var scale = Mathf.Lerp(def.S0, def.S1, e);
            var alpha = Mathf.Lerp(def.A0, def.A1, fade);
            var tint = def.Tint;
            var rotation = def.Rot * e;
            var position = Vector2.Zero;

            switch (def.Kind)
            {
                case LayerKind.Glow:
                case LayerKind.Flash:
                    var rise = def.Kind == LayerKind.Glow ? 0.25f : 0.30f;
                    alpha = local < rise
                        ? def.A0 * (local / rise)
                        : def.A0 * (1f - Mathf.Pow((local - rise) / (1f - rise), def.AlphaPow));
                    break;
                case LayerKind.Note:
                    scale = Mathf.Lerp(def.S0, def.S1, EaseOutCubic(Mathf.Min(local / 0.18f, 1f)));
                    scale *= 1f
                        + 0.10f * Mathf.Exp(-Mathf.Pow((seconds - 0.32f) / 0.05f, 2f))
                        + 0.05f * Mathf.Exp(-Mathf.Pow((seconds - 0.50f) / 0.05f, 2f));
                    var tail = Mathf.Clamp((seconds - Life * 0.85f) / (Life * 0.15f), 0f, 1f);
                    alpha = 1f - Mathf.Pow(tail, def.AlphaPow);
                    break;
                case LayerKind.Glyph:
                    if (seconds < GatherEnd)
                    {
                        var u = Smooth(seconds / GatherEnd);
                        position = Orbit(def.Theta + 40f * u, Mathf.Lerp(RadiusOut, RadiusIn, u));
                        scale = Mathf.Lerp(def.S0, def.SMid, u);
                        alpha = Mathf.Lerp(0f, def.A0, u);
                        rotation = 40f * u;
                    }
                    else if (seconds < HoldEnd)
                    {
                        position = Orbit(def.Theta + 40f, RadiusIn);
                        scale = def.SMid;
                        alpha = 0.90f;
                        rotation = 40f;
                    }
                    else
                    {
                        var u = Mathf.Clamp((seconds - HoldEnd) / (EjectEnd - HoldEnd), 0f, 1f);
                        var eo = EaseOutCubic(u);
                        position = Orbit(def.Theta + 40f + 55f * eo, Mathf.Lerp(RadiusIn, RadiusEject, eo));
                        scale = Mathf.Lerp(def.SMid, def.S1, eo);
                        alpha = Mathf.Lerp(0.95f, 0f, Mathf.Pow(u, def.AlphaPow));
                        rotation = 40f + 55f * eo;
                        tint = def.Tint.Lerp(def.TintOut, eo);
                    }
                    break;
            }

            node.Scale = baseScale * scale;
            node.Rotation = baseRotation + Mathf.DegToRad(rotation);
            node.Position = position;
            node.Modulate = new Color(tint.R, tint.G, tint.B, alpha);
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

    private static Vector2 Orbit(float degrees, float radius)
    {
        var rad = Mathf.DegToRad(degrees);
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
    }

    private static float Smooth(float x) => x * x * (3f - 2f * x);

    private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
}