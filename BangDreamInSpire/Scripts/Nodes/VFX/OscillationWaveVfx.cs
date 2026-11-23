using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace BangDreamInSpire.Scripts.Nodes.VFX;

/// <summary>震荡施法与余音回收表现，不参与结算。</summary>
[Tool]
public partial class OscillationWaveVfx : Node2D
{
    private const string ScenePath = "res://BangDreamInSpire/scenes/vfx/oscillation_wave_vfx.tscn";

    private const float Life = 0.62f;

    /// <summary>表现层参数。</summary>
    private sealed record LayerDef(
        string NodePath, Color Tint, float T0, float T1,
        float S0, float S1, float A0, float A1, float Rot, float AlphaPow);

    private static readonly LayerDef[] Layers =
    [
        new("GroundGlow", new Color(0.62f, 0.72f, 0.95f), 0.00f, 0.75f, 1.20f, 2.20f, 0.30f, 0f, 0f, 1.80f),
        new("Score", new Color(0.72f, 0.80f, 1.00f), 0.00f, 0.50f, 0.35f, 1.20f, 0.90f, 0f, 18f, 1.60f),
        new("WaveA", new Color(0.70f, 0.78f, 1.00f), 0.04f, 0.85f, 0.55f, 2.10f, 0.85f, 0f, -10f, 1.90f),
        new("WaveB", new Color(0.66f, 0.76f, 1.00f), 0.10f, 0.95f, 0.90f, 2.45f, 0.70f, 0f, 0f, 1.40f),
        new("Echo", new Color(0.72f, 0.80f, 1.00f), 0.62f, 0.96f, 1.00f, 0.42f, 0.60f, 0f, 12f, 2.00f),
    ];

    private readonly List<(Sprite2D Node, LayerDef Def, Vector2 BaseScale, float BaseRotation)> _layers = [];

    private Tween? _tween;

    /// <summary>入树后应用的施法者世界坐标。</summary>
    private Vector2 _spawnWorld;

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

    [Export] public bool LoopPreview { get; set; } = true;

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
        // 从场景读取美术基准。
        foreach (var def in Layers)
        {
            if (GetNodeOrNull<Sprite2D>(def.NodePath) is not { } sprite)
                continue;

            _layers.Add((sprite, def, sprite.Scale, sprite.Rotation));
        }

        // 入树后再应用世界坐标。
        if (_spawnWorld != Vector2.Zero)
            GlobalPosition = _spawnWorld;

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

        // 所有状态由单一 TweenMethod 驱动。
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

            // 未到登场时间的层保持隐藏。
            if (clamped < def.T0)
            {
                node.Scale = baseScale * def.S0;
                node.Modulate = new Color(def.Tint.R, def.Tint.G, def.Tint.B, 0f);
                continue;
            }

            var eScale = EaseOutCubic(local);
            // fade 必须从 0 递增到 1。
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
