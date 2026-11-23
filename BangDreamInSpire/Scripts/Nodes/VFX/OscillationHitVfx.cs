using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace BangDreamInSpire.Scripts.Nodes.VFX;

/// <summary>震荡命中与音符回收表现，不参与结算。</summary>
[Tool]
public partial class OscillationHitVfx : Node2D
{
    private const string ScenePath = "res://BangDreamInSpire/scenes/vfx/oscillation_hit_vfx.tscn";

    private const float Life = 0.52f;

    private const float NoteRiseTime = 0.04f;

    /// <summary>音符参数，顺序对应 Note0 / Note1。</summary>
    private static readonly float[] NoteStart = [0.07f, 0.12f];
    private static readonly float[] NoteFly = [0.34f, 0.34f];
    private static readonly float[] NoteBend = [70f, -70f];
    private static readonly float[] NoteRotateDegrees = [30f, -24f];
    private static readonly float[] NoteAlpha = [0.95f, 0.90f];

    private static readonly Vector2[] NoteStartOffset = [new(0f, -6f), new(14f, -22f)];

    /// <summary>表现层参数；颜色以场景为基准。</summary>
    private sealed record LayerDef(
        string NodePath, float T0, float T1,
        float S0, float S1, float A0, float A1, float Rot, float AlphaPow);

    private static readonly LayerDef[] Layers =
    [
        new("CoreGlow", 0.00f, 0.30f, 0.45f, 1.05f, 0.95f, 0f, 0f, 1.60f),
        new("RingA", 0.02f, 0.62f, 0.18f, 0.72f, 0.95f, 0f, -14f, 1.90f),
        new("RingB", 0.05f, 0.58f, 0.12f, 0.88f, 0.90f, 0f, 4f, 1.70f),
        new("Star", 0.00f, 0.20f, 0.40f, 0.75f, 1.00f, 0f, 12f, 1.20f),
    ];

    private readonly List<(Sprite2D Node, LayerDef Def, Vector2 BaseScale, float BaseRotation, Color BaseModulate)>
        _layers = [];

    private readonly List<NoteTrack> _notes = [];

    /// <summary>施法者世界坐标，作为回收终点。</summary>
    private Vector2 _casterWorld;

    /// <summary>入树后应用的目标世界坐标。</summary>
    private Vector2 _spawnWorld;

    private Tween? _tween;

    private sealed record NoteTrack(
        Sprite2D Node, Vector2 BaseScale, float BaseRotation, Color BaseModulate,
        Vector2 P0, Vector2 P1, Vector2 Control);

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

    /// <summary>创建目标命中特效。</summary>
    public static OscillationHitVfx? Create(Creature target, Creature? caster)
    {
        if (TestMode.IsOn)
            return null;

        if (NCombatRoom.Instance?.GetCreatureNode(target) is not { } targetNode)
            return null;

        var vfx = BangDreamPreloadManager.GetScene(ScenePath).Instantiate<OscillationHitVfx>();
        vfx._spawnWorld = targetNode.VfxSpawnPosition;
        vfx._casterWorld = caster != null && NCombatRoom.Instance.GetCreatureNode(caster) is { } casterNode
            ? casterNode.VfxSpawnPosition
            : targetNode.VfxSpawnPosition + new Vector2(-280f, -80f);
        return vfx;
    }

    public override void _Ready()
    {
        // 从场景读取美术基准。
        foreach (var def in Layers)
        {
            if (GetNodeOrNull<Sprite2D>(def.NodePath) is not { } sprite)
                continue;

            _layers.Add((sprite, def, sprite.Scale, sprite.Rotation, sprite.Modulate));
        }

        // 入树后再应用世界坐标。
        if (_spawnWorld != Vector2.Zero)
            GlobalPosition = _spawnWorld;

        // 定位后将施法者坐标转换为本地坐标。
        var casterLocal = ToLocal(_casterWorld) + new Vector2(0f, -18f);
        var noteNodes = new[] { GetNodeOrNull<Sprite2D>("Notes/Note0"), GetNodeOrNull<Sprite2D>("Notes/Note1") };

        for (var i = 0; i < noteNodes.Length; i++)
        {
            if (noteNodes[i] is not { } note)
                continue;

            var p0 = note.Position + NoteStartOffset[i];
            note.Position = p0;
            var p1 = casterLocal + new Vector2(0f, i * -6f);
            var direction = p1 - p0;
            // 两枚音符向相反方向弯曲。
            var normal = new Vector2(-direction.Y, direction.X).Normalized();
            var control = (p0 + p1) * 0.5f + normal * NoteBend[i];
            _notes.Add(new NoteTrack(note, note.Scale, note.Rotation, note.Modulate, p0, p1, control));
        }

        // 仅运行态播放。
        if (VfxPreviewSupport.AutoPlayOnReady)
            Replay();
    }

    public void Replay()
    {
        _tween?.Kill();

        VfxPreviewSupport.CenterForPreview(this);

        foreach (var (node, _, baseScale, baseRotation, baseModulate) in _layers)
        {
            node.Scale = baseScale;
            node.Rotation = baseRotation;
            node.Modulate = baseModulate;
        }

        foreach (var note in _notes)
        {
            note.Node.Position = note.P0;
            note.Node.Scale = note.BaseScale;
            note.Node.Rotation = note.BaseRotation;
            note.Node.Modulate = note.BaseModulate;
            note.Node.Visible = false;
        }

        // 所有状态由单一 TweenMethod 驱动。
        _tween = CreateTween();
        _tween.TweenMethod(Callable.From<float>(ApplyState), 0f, 1f, Life);
        _tween.Finished += OnPlaybackFinished;
    }

    private void ApplyState(float progress)
    {
        var clamped = Mathf.Clamp(progress, 0f, 1f);
        var time = clamped * Life;

        foreach (var (node, def, baseScale, baseRotation, baseModulate) in _layers)
        {
            var span = Mathf.Max(def.T1 - def.T0, 0.0001f);
            var local = Mathf.Clamp((clamped - def.T0) / span, 0f, 1f);

            // 未到登场时间的层保持隐藏。
            if (clamped < def.T0)
            {
                node.Scale = baseScale * def.S0;
                node.Modulate = new Color(baseModulate.R, baseModulate.G, baseModulate.B, 0f);
                continue;
            }

            var eScale = EaseOutCubic(local);
            // fade 必须从 0 递增到 1。
            var fade = Mathf.Pow(local, def.AlphaPow);

            node.Scale = baseScale * Mathf.Lerp(def.S0, def.S1, eScale);
            node.Rotation = baseRotation + Mathf.DegToRad(def.Rot * eScale);
            node.Modulate = new Color(baseModulate.R, baseModulate.G, baseModulate.B,
                Mathf.Lerp(def.A0, def.A1, fade));
        }

        ApplyNotes(time);
    }

    private void ApplyNotes(float time)
    {
        for (var i = 0; i < _notes.Count; i++)
        {
            var note = _notes[i];
            var start = NoteStart[i];
            var fly = NoteFly[i];
            var visible = time >= start && time < start + fly;
            note.Node.Visible = visible;
            if (!visible)
                continue;

            var local = Mathf.Clamp((time - start) / fly, 0f, 1f);
            // 使用先慢、中快、落慢的回收节奏。
            var u = local * local * (3f - 2f * local);

            var position = QuadraticBezier(note.P0, note.Control, note.P1, u);
            // 叠加轻微上下浮动。
            position.Y += Mathf.Sin(local * Mathf.Pi * 2f) * 3f;
            note.Node.Position = position;

            var rise = Mathf.Clamp((time - start) / NoteRiseTime, 0f, 1f);
            var fadeOut = 1f - Mathf.Clamp((local - 0.76f) / 0.24f, 0f, 1f);
            var scale = Mathf.Lerp(1f, 0.55f, u) * Mathf.Lerp(0.45f, 1f, rise);
            note.Node.Scale = note.BaseScale * scale;
            note.Node.Rotation = note.BaseRotation + Mathf.DegToRad(NoteRotateDegrees[i] * u);
            note.Node.Modulate = new Color(note.BaseModulate.R, note.BaseModulate.G, note.BaseModulate.B,
                rise * fadeOut * NoteAlpha[i]);
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

    private static Vector2 QuadraticBezier(Vector2 p0, Vector2 control, Vector2 p1, float u)
    {
        var inverse = 1f - u;
        return inverse * inverse * p0 + 2f * inverse * u * control + u * u * p1;
    }

    private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
}
