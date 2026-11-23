using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace BangDreamInSpire.Scripts.Nodes.VFX;

/// <summary>
/// 震荡命中：迎面波前压到目标身上塌成一条线 → 目标起驻波节线并按拍频跳动 → 节线碎裂
/// → 一束反向波带着音符回波飞回演奏者。纯表现，不参与结算。
/// 声学语汇见 docs/mechanic-authoring.md「震荡」章节；回波音符数量 = 该目标给与的余音数。
/// </summary>
[Tool]
public partial class OscillationHitVfx : Node2D
{
    private const string ScenePath = "res://BangDreamInSpire/scenes/vfx/oscillation_hit_vfx.tscn";

    private const float Life = 0.58f;

    /// <summary>身体框的设计基准（与 PerformBodyVfx 同口径），用于把节线花贴合目标体型。</summary>
    private static readonly Vector2 DesignBodySize = new(320f, 300f);

    /// <summary>迎面波前：从外压到目标身上的时刻。</summary>
    private const float FrontStart = 0.00f;

    private const float FrontEnd = 0.11f;

    /// <summary>压平后的余振结束时刻。</summary>
    private const float FrontSettleEnd = 0.30f;

    /// <summary>反射驻波条纹：从表面压出来到散尽。</summary>
    private const float FringeStart = 0.03f;

    private const float FringeEnd = 0.30f;

    /// <summary>拍频：条纹亮度跳动的次数与衰减速度。</summary>
    private const float FringeBeatCount = 3f;

    private const float FringeBeatDecay = 2.6f;

    /// <summary>节线断裂的碎片飞出。</summary>
    private const float ShardStart = 0.15f;

    private const float ShardLife = 0.24f;

    /// <summary>反向波前（反射波）。</summary>
    private const float ReflectionStart = 0.13f;

    private const float ReflectionEnd = 0.38f;

    /// <summary>回波音符：首枚出发时刻与单枚飞行时长。</summary>
    private const float NoteStart = 0.17f;

    private const float NoteStagger = 0.055f;

    private const float NoteFly = 0.24f;

    /// <summary>回波抵达演奏者：弦再亮一次。</summary>
    private const float AbsorbStart = 0.41f;

    private const float AbsorbEnd = 0.55f;

    private const int ShardCount = 6;

    /// <summary>场景里预摆的回波音符槽位数（实际只用前 _noteCount 个）。</summary>
    private const int MaxNotes = 3;

    private const float NoteScale = 1f;

    /// <summary>回波球的壳色：同族蓝里的三个分音色温，逐球微差。</summary>
    private static readonly Color[] BallShellColors =
    [
        new(0.45f, 0.72f, 1.00f),
        new(0.54f, 0.64f, 1.00f),
        new(0.40f, 0.79f, 1.00f),
    ];

    private const float DesignFrontWidth = 300f;

    private const float DesignTextureSize = 256f;

    private readonly List<ShardTrack> _shards = [];
    private readonly List<NoteTrack> _notes = [];

    private ColorRect _front = null!;
    private ColorRect _reflection = null!;
    private ColorRect _fringe = null!;
    private Sprite2D _core = null!;
    private Node2D _absorb = null!;
    private Sprite2D _absorbRing = null!;

    private Vector2 _coreBaseScale;
    private Color _coreBaseColor = Colors.White;
    private Vector2 _absorbRingBaseScale;
    private Color _absorbRingBaseColor = Colors.White;

    /// <summary>入树后应用的目标世界坐标。</summary>
    private Vector2 _spawnWorld;

    /// <summary>演奏者在本节点局部坐标系里的位置（回波终点）。</summary>
    private Vector2 _casterLocal = new(-360f, -60f);

    /// <summary>波前方向（弧度）：由演奏者指向目标。</summary>
    private float _incomingAngle;

    /// <summary>体型缩放：节线花与碎片按目标实际体格铺开。</summary>
    private float _bodyFit = 1f;

    /// <summary>本目标命中后给与的余音数量，决定回波音符枚数。</summary>
    private int _noteCount = 1;

    private Tween? _tween;

    private sealed record ShardTrack(
        Sprite2D Node, Vector2 Start, float Spin, Vector2 BaseScale, Color BaseColor);

    private sealed record NoteTrack(
        Node2D Node, Control Orb, Control Trail, float Delay,
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

    /// <summary>创建命中表现；perEnemyGain 为该目标给与的余音数量。</summary>
    public static OscillationHitVfx? Create(Creature target, Creature? caster, int perEnemyGain)
    {
        if (TestMode.IsOn)
            return null;

        var room = NCombatRoom.Instance;
        if (room?.GetCreatureNode(target) is not { } targetNode)
            return null;

        var vfx = BangDreamPreloadManager.GetScene(ScenePath).Instantiate<OscillationHitVfx>();
        vfx._spawnWorld = targetNode.VfxSpawnPosition;

        // 节线花贴合目标体格（命中框），与演奏身印同一套口径。
        var size = targetNode.Hitbox.Size;
        var fit = size is { X: > 0f, Y: > 0f }
            ? (size.X / DesignBodySize.X + size.Y / DesignBodySize.Y) * 0.5f
            : 1f;
        vfx._bodyFit = Mathf.Clamp(fit, 0.55f, 1.9f);
        vfx._noteCount = Mathf.Clamp(perEnemyGain, 1, 3);

        if (caster != null && room.GetCreatureNode(caster) is { } casterNode)
        {
            vfx._casterLocal = casterNode.VfxSpawnPosition - vfx._spawnWorld + new Vector2(0f, -24f);
            vfx._incomingAngle = (vfx._spawnWorld - casterNode.VfxSpawnPosition).Angle();
        }
        else
        {
            vfx._casterLocal = new Vector2(-360f, -40f);
            vfx._incomingAngle = 0f;
        }

        return vfx;
    }

    /// <summary>
    /// 合成预览用的接缝：手工指定回波终点（演奏者在局部坐标里的位置）与来波方向。
    /// 游戏内由 <see cref="Create"/> 注入，这里供把特效摆进合成场景时使用，
    /// 与 PerformBodyVfx.ConfigureBody 同一类用法。
    /// </summary>
    public void ConfigureStage(Vector2 casterLocal, float incomingAngle, float bodyFit = 1f, int noteCount = 1)
    {
        _casterLocal = casterLocal;
        _incomingAngle = incomingAngle;
        _bodyFit = bodyFit;
        _noteCount = Mathf.Clamp(noteCount, 1, _notes.Count > 0 ? _notes.Count : noteCount);
        if (IsNodeReady())
            Replay();
    }

    public override void _Ready()
    {
        _front = GetNode<ColorRect>("Front");
        _reflection = GetNode<ColorRect>("Reflection");
        _fringe = GetNode<ColorRect>("Fringe");
        _core = GetNode<Sprite2D>("Core");
        _absorb = GetNode<Node2D>("Absorb");
        _absorbRing = GetNode<Sprite2D>("Absorb/Ring");

        // 只读美术基准，编辑器里不改变视觉状态。
        _coreBaseScale = _core.Scale;
        _coreBaseColor = _core.Modulate;
        _absorbRingBaseScale = _absorbRing.Scale;
        _absorbRingBaseColor = _absorbRing.Modulate;

        for (var i = 0; i < ShardCount; i++)
        {
            if (GetNodeOrNull<Sprite2D>($"Shards/Shard{i}") is not { } shard)
                continue;

            _shards.Add(new ShardTrack(shard, shard.Position, 0f, shard.Scale, shard.Modulate));
        }

        // 槽位全部收集，实际枚数由 _noteCount 决定（预览态是 2）。
        for (var i = 0; i < MaxNotes; i++)
        {
            if (GetNodeOrNull<Node2D>($"Notes/Note{i}") is not { } note ||
                GetNodeOrNull<Control>($"Notes/Note{i}/Orb") is not { } orb ||
                GetNodeOrNull<Control>($"Notes/Note{i}/Trail") is not { } trail)
                continue;

            _notes.Add(new NoteTrack(note, orb, trail, 0f, Vector2.Zero, Vector2.Zero, Vector2.Zero));
        }

        _noteCount = Mathf.Clamp(_noteCount, 1, _notes.Count);

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

        if (VfxPreviewSupport.IsPreviewRun(this))
            SetUpPreview();

        ApplyGeometry();
        ResetVisuals();

        _tween = CreateTween();
        _tween.TweenMethod(Callable.From<float>(ApplyState), 0f, 1f, Life);
        _tween.Finished += OnPlaybackFinished;
    }

    /// <summary>预览：把「演奏者」摆在左前方，波从前方向右压入，回波飞回左侧。</summary>
    private void SetUpPreview()
    {
        VfxPreviewSupport.CenterForPreview(this);
        Position += new Vector2(220f, 0f);
        _casterLocal = new Vector2(-620f, -30f);
        _incomingAngle = 0f;
        _bodyFit = 1f;
        _noteCount = Mathf.Clamp(2, 1, _notes.Count);
    }

    /// <summary>按方向与体型铺开四块 shader 画布。</summary>
    private void ApplyGeometry()
    {
        // 迎面波前：竖带垂直于来波方向，自带旋转让局部 +x 就是传播方向。
        // 画布宽度要盖住全程 offset + bow：否则波带会从画布侧面「凭空出现」。
        var frontSize = new Vector2(DesignFrontWidth * 1.8f, 392f);
        ConfigureQuad(_front, frontSize);
        _front.Rotation = _incomingAngle;

        // 反向波前：同一支 shader 转 180°，局部 +x 变成「回演奏者」的方向。
        // 宽度同样要盖住 offset(0→300) + bow(96)，否则回推的波带会被画布侧面切断。
        var reflectSize = new Vector2(900f, 620f);
        ConfigureQuad(_reflection, reflectSize);
        _reflection.Rotation = _incomingAngle + Mathf.Pi;

        // 反射驻波：画布横向只用到「表面之前」那半边，转到来波方向上。
        var fringeSize = new Vector2(760f, DesignBodySize.Y * _bodyFit * 1.25f);
        ConfigureQuad(_fringe, fringeSize);
        _fringe.Rotation = _incomingAngle;

        // 回波音符的航路：从命中点出发，向外弓一下再飞向演奏者。
        var launch = new Vector2(0f, -6f);
        var travel = _casterLocal - launch;
        var normal = new Vector2(-travel.Y, travel.X).Normalized();
        for (var i = 0; i < _noteCount; i++)
        {
            var track = _notes[i];
            var bend = (i % 2 == 0 ? 1f : -1f) * Mathf.Lerp(96f, 52f, i / 2f);
            _notes[i] = track with
            {
                Delay = NoteStart + i * NoteStagger,
                P0 = launch + new Vector2(-8f + i * 14f, -14f + i * 10f),
                P1 = _casterLocal + new Vector2(0f, i * -8f),
                Control = launch + travel * 0.5f + normal * bend,
            };

            // 逐球画布：三颗球尺寸略差（不同分音），拖尾长度跟着球走。
            var orbSize = OrbSizeFor(i);
            var shell = BallShellColors[i % BallShellColors.Length];
            ConfigureQuad(track.Orb, new Vector2(orbSize, orbSize));
            track.Orb.SetInstanceShaderParameter("shell_color", shell);
            track.Orb.SetInstanceShaderParameter("core_color", (shell * 0.35f).Lerp(Colors.White, 0.72f));
            ConfigureQuad(track.Trail, new Vector2(orbSize * 0.95f, orbSize * 0.34f));
            track.Trail.SetInstanceShaderParameter("tint", shell);
        }

        _absorb.Position = _casterLocal;
    }

    /// <summary>
    /// 摆好一块 shader 画布：尺寸、以中心为轴（Control 的旋转轴是 pivot 而非中心，
    /// 不设 pivot 直接旋转会把整块画布平移到别处 —— 录帧表现为「半空漂着一条光带」），
    /// 以及与像素尺寸同源的 instance uniform。
    /// </summary>
    private static void ConfigureQuad(Control quad, Vector2 size)
    {
        quad.Position = -size * 0.5f;
        quad.Size = size;
        quad.PivotOffset = size * 0.5f;
        quad.SetInstanceShaderParameter("quad_size", size);
    }

    private void ResetVisuals()
    {
        _core.Scale = _coreBaseScale;
        _core.Modulate = _coreBaseColor;
        _absorbRing.Scale = _absorbRingBaseScale;
        _absorbRing.Modulate = _absorbRingBaseColor;

        foreach (var shard in _shards)
        {
            shard.Node.Position = shard.Start;
            shard.Node.Scale = shard.BaseScale;
            shard.Node.Modulate = shard.BaseColor;
            shard.Node.Visible = false;
        }

        for (var i = 0; i < _notes.Count; i++)
        {
            var note = _notes[i];
            note.Node.Position = note.P0;
            note.Node.Scale = Vector2.One * NoteScale;
            note.Node.Modulate = Colors.White;
            note.Node.Visible = false;
        }
    }

    private void ApplyState(float progress)
    {
        var time = Mathf.Clamp(progress, 0f, 1f) * Life;

        ApplyFront(time);
        ApplyFringe(time);
        ApplyCore(time);
        ApplyShards(time);
        ApplyReflection(time);
        ApplyNotes(time);
        ApplyAbsorb(time);
    }

    /// <summary>
    /// 迎面波前：从外压到目标身上，弧被压平成一条竖线；压平后这条线贴着表面
    /// 左右急颤、振幅衰减 —— 这就是「震荡」本体，也是全片唯一的直线。
    /// </summary>
    private void ApplyFront(float time)
    {
        float offset;
        float bow;
        float thickness;
        float intensity;

        if (time < FrontEnd)
        {
            var local = Mathf.Clamp(time / FrontEnd, 0f, 1f);
            var travel = EaseInQuad(local);
            offset = Mathf.Lerp(-DesignFrontWidth * 0.86f, 0f, travel);
            bow = Mathf.Lerp(56f, 0f, travel);
            thickness = Mathf.Lerp(26f, 9f, local);
            intensity = Mathf.Clamp(local / 0.16f, 0f, 1f);
        }
        else
        {
            var span = FrontSettleEnd - FrontEnd;
            var local = Mathf.Clamp((time - FrontEnd) / span, 0f, 1f);
            var decay = Mathf.Exp(-3.1f * local);
            offset = Mathf.Sin(local * Mathf.Pi * 2f * 2.5f) * 10f * decay;
            bow = 0f;
            thickness = Mathf.Lerp(9f, 17f, local);
            intensity = Mathf.Pow(1f - local, 1.2f) * 0.95f;
        }

        _front.SetInstanceShaderParameter("offset", offset);
        _front.SetInstanceShaderParameter("bow", bow);
        _front.SetInstanceShaderParameter("thickness", thickness);
        _front.SetInstanceShaderParameter("intensity", intensity);
    }

    /// <summary>反射驻波：条纹贴在目标表面、间距向外变宽，亮度按拍频跳动后散尽。</summary>
    private void ApplyFringe(float time)
    {
        var span = FringeEnd - FringeStart;
        var local = Mathf.Clamp((time - FringeStart) / span, 0f, 1f);
        if (time < FringeStart)
        {
            _fringe.SetInstanceShaderParameter("intensity", 0f);
            return;
        }

        // 拍频：响 — 弱 — 更弱，与波形自身的「一阵一阵」对齐。
        var beat = Mathf.Exp(-FringeBeatDecay * local) *
                   Mathf.Abs(Mathf.Cos(Mathf.Pi * local * FringeBeatCount));
        _fringe.SetInstanceShaderParameter("pulse", beat);
        // 条纹间距随反射波离开而拉开（表面处的半波长在放松）。
        _fringe.SetInstanceShaderParameter("spacing", Mathf.Lerp(78f, 112f, local));
        _fringe.SetInstanceShaderParameter("intensity", Mathf.Pow(1f - local, 1.15f) * (0.55f + 0.75f * beat));
    }

    /// <summary>接触点的高压核心：极短、极小，只负责把峰值点亮。</summary>
    private void ApplyCore(float time)
    {
        var local = Mathf.Clamp(time / 0.11f, 0f, 1f);
        _core.Scale = _coreBaseScale * Mathf.Lerp(0.45f, 1.35f, EaseOutCubic(local));
        _core.Modulate = new Color(_coreBaseColor.R, _coreBaseColor.G, _coreBaseColor.B,
            _coreBaseColor.A * (1f - local) * (1f - local));
    }

    /// <summary>节线断裂：碎片沿节线方向甩出并受重力，短促、有速度差。</summary>
    private void ApplyShards(float time)
    {
        for (var i = 0; i < _shards.Count; i++)
        {
            var shard = _shards[i];
            var local = Mathf.Clamp((time - ShardStart) / ShardLife, 0f, 1f);
            var visible = time >= ShardStart && local < 1f;
            shard.Node.Visible = visible;
            if (!visible)
                continue;

            var velocity = ShardVelocity(i);
            // 起跑点自带一小段位移：碎片是从断裂的节线上崩出来的，不在中心堆成一团。
            var travel = local * ShardLife + 0.035f;
            shard.Node.Position = shard.Start + velocity * travel +
                                  new Vector2(0f, 240f * travel * travel);
            // 碎晶贴图的长轴朝右上（-45°），旋转对齐速度方向才像被崩飞。
            shard.Node.Rotation = velocity.Angle() + Mathf.Pi * 0.25f + shard.Spin * local;
            shard.Node.Scale = shard.BaseScale * Mathf.Lerp(1.25f, 0.5f, local);
            shard.Node.Modulate = new Color(shard.BaseColor.R, shard.BaseColor.G, shard.BaseColor.B,
                shard.BaseColor.A * (1f - local * local));
        }
    }

    /// <summary>反射波：从目标身上朝演奏者推回去，越走越散越淡。</summary>
    private void ApplyReflection(float time)
    {
        var span = ReflectionEnd - ReflectionStart;
        var local = Mathf.Clamp((time - ReflectionStart) / span, 0f, 1f);
        _reflection.SetInstanceShaderParameter("offset", Mathf.Lerp(-10f, 300f, EaseOutCubic(local)));
        _reflection.SetInstanceShaderParameter("bow", Mathf.Lerp(18f, 96f, local));
        _reflection.SetInstanceShaderParameter("thickness", Mathf.Lerp(30f, 76f, local));
        _reflection.SetInstanceShaderParameter("intensity",
            time < ReflectionStart ? 0f : Mathf.Sin(Mathf.Pi * Mathf.Pow(local, 0.72f)) * 0.72f);
    }

    /// <summary>回波球：沿弧线飞回演奏者，抵达时收束成一点。</summary>
    private void ApplyNotes(float time)
    {
        // 只动实际使用的槽位：未配置的槽位 Delay 为 0，若一并参与就会在命中点凭空多一颗球。
        for (var i = 0; i < _noteCount; i++)
        {
            var note = _notes[i];
            var local = Mathf.Clamp((time - note.Delay) / NoteFly, 0f, 1f);
            var visible = time >= note.Delay && local < 1f;
            note.Node.Visible = visible;
            if (!visible)
                continue;

            // 先慢后快：回波是被「吸」回演奏者的。
            var u = Mathf.Lerp(local, EaseInQuad(local), 0.55f);
            var position = QuadraticBezier(note.P0, note.Control, note.P1, u);
            var travelDirection = QuadraticBezier(note.P0, note.Control, note.P1,
                Mathf.Min(u + 0.04f, 1f)) - position;
            position.Y += Mathf.Sin(local * Mathf.Pi * 2f) * 4f;
            note.Node.Position = position;

            var travelUnit = travelDirection.LengthSquared() > 0.001f
                ? travelDirection.Normalized()
                : Vector2.Left;

            // 姿态：整颗球按航向拉长一点（速度感）；拖尾作为子节点就跟着走，不必各自旋转。
            note.Node.Rotation = travelUnit.Angle();
            var pop = Mathf.Lerp(0.55f, 1f, Mathf.Clamp(local / 0.22f, 0f, 1f));
            var shrink = Mathf.Lerp(1f, 0.68f, Mathf.Clamp((local - 0.72f) / 0.28f, 0f, 1f));
            var size = NoteScale * pop * shrink;
            note.Node.Scale = new Vector2(size * 1.1f, size * 0.93f);

            var fadeIn = Mathf.Clamp(local / 0.14f, 0f, 1f);
            var fadeOut = 1f - Mathf.Clamp((local - 0.84f) / 0.16f, 0f, 1f);
            note.Node.Modulate = new Color(1f, 1f, 1f, fadeIn * fadeOut);

            // 球里在响：呼吸与内部条纹相位随飞行推进，且逐球错开。
            note.Orb.SetInstanceShaderParameter("phase", local + i * 0.23f);

            // 短拖尾：球节点已按航向旋转，拖尾只要在局部 -x 方向铺开即可。
            // 拖尾整块往球后推半个球径，让它的前缘埋在球体里 —— 前缘落在球心上
            // 会在球中间画出一条硬分界（录帧最明显的就是这一条）。
            note.Trail.PivotOffset = note.Trail.Size * 0.5f;
            note.Trail.Position = new Vector2(-note.Trail.Size.X * 0.5f - OrbSizeFor(i) * 0.16f, 0f)
                    - note.Trail.Size * 0.5f;
            note.Trail.SetInstanceShaderParameter("strength", 0.78f);
        }
    }

    /// <summary>抵达：弦再亮一次 + 一圈收束环 —— 回波在这里变成余音。</summary>
    private void ApplyAbsorb(float time)
    {
        var span = AbsorbEnd - AbsorbStart;
        var local = Mathf.Clamp((time - AbsorbStart) / span, 0f, 1f);
        var visible = time >= AbsorbStart;
        _absorb.Visible = visible;
        if (!visible)
            return;

        var pop = Mathf.Sin(Mathf.Pi * Mathf.Clamp(local * 1.4f, 0f, 1f));
        _absorbRing.Scale = _absorbRingBaseScale * Mathf.Lerp(1.5f, 0.25f, EaseOutCubic(local));
        _absorbRing.Modulate = new Color(_absorbRingBaseColor.R, _absorbRingBaseColor.G,
            _absorbRingBaseColor.B, _absorbRingBaseColor.A * pop);

        // 抵达即「收进身上」：环从外收束到一点，不再补竖直的弦（会读成禁止符）。
    }

    /// <summary>第 i 颗回波球的画布边长（px）：可见球径约为它的 0.42，三颗不等大读成不同分音。</summary>
    private static float OrbSizeFor(int index) => 131f - index * 13f;

    private Vector2 ShardVelocity(int index)
    {
        // 被压向前的碎片占多数，其余向两侧甩；速度与尺寸刻意不齐。
        var spread = index switch
        {
            0 => -58f,
            1 => 18f,
            2 => -14f,
            3 => 62f,
            4 => 34f,
            _ => -34f,
        };
        var angleDegrees = _incomingAngle * (180f / Mathf.Pi) + spread;
        var speed = 720f + index * 88f;
        return Vector2.FromAngle(Mathf.DegToRad(angleDegrees)) * speed * Mathf.Lerp(0.8f, 1.1f, index / 5f);
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

    private static float EaseInQuad(float x) => x * x;
}
