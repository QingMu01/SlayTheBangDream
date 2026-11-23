using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace BangDreamInSpire.Scripts.Nodes.VFX;

/// <summary>
/// 激荡命中：<c>2^i</c> 道波前一起压平到目标表面、驻波条纹按渐强跳动、碎晶甩出，余音球飞回演奏者。
/// 纯表现，不参与结算；逐击翻倍的层数与参数见 docs/mechanic-authoring.md 12.4。
/// </summary>
[Tool]
public partial class SurgeHitVfx : Node2D
{
    private const string ScenePath = "res://BangDreamInSpire/scenes/vfx/surge_hit_vfx.tscn";

    /// <summary>身体框设计基准，用于把驻波与碎片贴合目标体型。</summary>
    private static readonly Vector2 DesignBodySize = new(320f, 300f);

    /// <summary>非末击的整幕时长。</summary>
    private const float Life = 0.60f;

    /// <summary>末击多留一段：波前穿出敌阵、散尽后再收。</summary>
    private const float FinalLife = 0.74f;

    /// <summary>波前只在表面附近压入：长距离飞行由施法幕的波包负责，命中幕不能把同一段路再走一遍。</summary>
    private const float ArriveFrom = -70f;

    private const float FrontArriveEnd = 0.045f;

    private const float FrontSettleEnd = 0.30f;

    /// <summary>叠层间距：抵达途中略收，但必须留出可数的一道道缝——道数就是这一击翻了几倍。</summary>
    private const float StackGapStart = 34f;

    private const float StackGapEnd = 20f;

    /// <summary>反射驻波的起止与跳动次数。</summary>
    private const float FringeStart = 0.03f;

    private const float FringeEnd = 0.32f;

    private const float FinalFringeEnd = 0.40f;

    private const float FringeBeatCount = 2f;

    /// <summary>碎晶甩出。</summary>
    private const float ShardStart = 0.13f;

    private const float ShardLife = 0.28f;

    private const int MaxShards = 12;

    /// <summary>余音回波：首枚出发时刻、单枚错开与飞行时长。</summary>
    private const float NoteStart = 0.17f;

    private const float NoteStagger = 0.05f;

    private const float NoteFly = 0.26f;

    private const float AbsorbStart = 0.44f;

    private const float AbsorbEnd = 0.58f;

    private const int MaxNotes = 3;

    /// <summary>末击的穿出波：起止与到达的横向距离。</summary>
    private const float ThroughStart = 0.17f;

    private const float ThroughEnd = 0.48f;

    private const float ThroughTravel = 900f;

    /// <summary>回波球的壳色：三个分音色温，与震荡同款（余音身份）。</summary>
    private static readonly Color[] BallShellColors =
    [
        new(0.45f, 0.72f, 1.00f),
        new(0.54f, 0.64f, 1.00f),
        new(0.40f, 0.79f, 1.00f),
    ];

    private readonly List<ShardTrack> _shards = [];
    private readonly List<NoteTrack> _notes = [];

    private ColorRect _front = null!;
    private ColorRect _through = null!;
    private ColorRect _fringe = null!;
    private Sprite2D _core = null!;
    private Node2D _absorb = null!;
    private Sprite2D _absorbRing = null!;

    private Vector2 _coreBaseScale = Vector2.One;
    private Color _coreBaseColor = Colors.White;
    private Vector2 _absorbRingBaseScale = Vector2.One;
    private Color _absorbRingBaseColor = Colors.White;

    /// <summary>入树后应用的目标世界坐标。</summary>
    private Vector2 _spawnWorld;

    /// <summary>演奏者在本节点局部坐标系里的位置（回波终点）。</summary>
    private Vector2 _casterLocal = new(-360f, -60f);

    /// <summary>波前方向（弧度）：从上一个敌人（或演奏者）指向目标。</summary>
    private float _incomingAngle;

    private float _bodyFit = 1f;

    /// <summary>本击是第几次命中（0 起）：决定叠层道数与全部"更重"的参数。</summary>
    private int _index;

    private bool _isFinal;

    private int _echoCount = MaxNotes;

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

    /// <summary>预览时展示第几击（0 起）：调观感时直接看最重的一击。</summary>
    [Export(PropertyHint.Range, "0,3,1")]
    public int PreviewIndex
    {
        get => _index;
        set
        {
            _index = Mathf.Clamp(value, 0, 3);
            if (VfxPreviewSupport.IsPreviewRun(this))
                Replay();
        }
    }

    /// <summary>创建一次命中表现；echoCount 为该击给与的余音数（决定回波球枚数）。</summary>
    public static SurgeHitVfx? Create(
        Creature target, Creature? caster, Creature? previousTarget, int index, bool isFinal, int echoCount)
    {
        if (TestMode.IsOn)
            return null;

        var room = NCombatRoom.Instance;
        if (room?.GetCreatureNode(target) is not { } targetNode)
            return null;

        var vfx = BangDreamPreloadManager.GetScene(ScenePath).Instantiate<SurgeHitVfx>();
        vfx._spawnWorld = targetNode.VfxSpawnPosition;

        // 驻波与碎片按目标命中框铺开，与演奏身印、震荡命中同一套口径。
        var size = targetNode.Hitbox.Size;
        var fit = size is { X: > 0f, Y: > 0f }
            ? (size.X / DesignBodySize.X + size.Y / DesignBodySize.Y) * 0.5f
            : 1f;
        vfx._bodyFit = Mathf.Clamp(fit, 0.55f, 1.9f);
        vfx._index = Mathf.Clamp(index, 0, 3);
        vfx._isFinal = isFinal;
        vfx._echoCount = Mathf.Clamp(echoCount, 0, MaxNotes);

        var fromNode = previousTarget != null ? room.GetCreatureNode(previousTarget) : null;
        if (caster != null && room.GetCreatureNode(caster) is { } casterNode)
            vfx._casterLocal = casterNode.VfxSpawnPosition - vfx._spawnWorld + new Vector2(0f, -24f);

        // 来波方向取"上一个敌人→目标"：链式接力时波是从它身上弹过来的。
        var fromPoint = fromNode?.VfxSpawnPosition ??
                        (caster != null && room.GetCreatureNode(caster) is { } c ? c.VfxSpawnPosition : null);
        if (fromPoint is { } origin)
            vfx._incomingAngle = (vfx._spawnWorld - origin).Angle();
        else
            vfx._casterLocal = new Vector2(-360f, -40f);

        return vfx;
    }

    /// <summary>合成预览用接缝：手工指定目标/演奏者世界坐标与来波方向。</summary>
    public void ConfigureStage(
        Vector2 targetWorld, Vector2 casterWorld, float incomingAngle, int index, bool isFinal, int echoCount)
    {
        _spawnWorld = targetWorld;
        _casterLocal = casterWorld - targetWorld + new Vector2(0f, -24f);
        _incomingAngle = incomingAngle;
        _index = Mathf.Clamp(index, 0, 3);
        _isFinal = isFinal;
        _echoCount = Mathf.Clamp(echoCount, 0, MaxNotes);
        if (IsNodeReady())
            Replay();
    }

    public override void _Ready()
    {
        _front = GetNode<ColorRect>("Front");
        _through = GetNode<ColorRect>("Through");
        _fringe = GetNode<ColorRect>("Fringe");
        _core = GetNode<Sprite2D>("Core");
        _absorb = GetNode<Node2D>("Absorb");
        _absorbRing = GetNode<Sprite2D>("Absorb/Ring");

        _coreBaseScale = _core.Scale;
        _coreBaseColor = _core.Modulate;
        _absorbRingBaseScale = _absorbRing.Scale;
        _absorbRingBaseColor = _absorbRing.Modulate;

        for (var i = 0; i < MaxShards; i++)
        {
            if (GetNodeOrNull<Sprite2D>($"Shards/Shard{i}") is not { } shard)
                continue;

            _shards.Add(new ShardTrack(shard, shard.Position, 0f, shard.Scale, shard.Modulate));
        }

        for (var i = 0; i < MaxNotes; i++)
        {
            if (GetNodeOrNull<Node2D>($"Notes/Note{i}") is not { } note ||
                GetNodeOrNull<Control>($"Notes/Note{i}/Orb") is not { } orb ||
                GetNodeOrNull<Control>($"Notes/Note{i}/Trail") is not { } trail)
                continue;

            _notes.Add(new NoteTrack(note, orb, trail, 0f, Vector2.Zero, Vector2.Zero, Vector2.Zero));
        }

        if (_spawnWorld != Vector2.Zero)
            GlobalPosition = _spawnWorld;

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
        _tween.TweenMethod(Callable.From<float>(ApplyState), 0f, 1f, _isFinal ? FinalLife : Life);
        _tween.Finished += OnPlaybackFinished;
    }

    /// <summary>预览摆位：演奏者在左前，波向右压入，回波飞回左侧。</summary>
    private void SetUpPreview()
    {
        VfxPreviewSupport.CenterForPreview(this);
        _casterLocal = new Vector2(-620f, -30f);
        _incomingAngle = 0f;
        _bodyFit = 1f;
        _isFinal = PreviewIndex >= 3;
        _echoCount = MaxNotes;
    }

    /// <summary>按来波方向与体型铺开四块 shader 画布。</summary>
    private void ApplyGeometry()
    {
        // 画布宽度要盖住 offset + 叠层总深，否则叠层会从画布侧面「凭空出现」。
        var frontSize = new Vector2(1100f, 440f * _bodyFit);
        ConfigureQuad(_front, frontSize);
        _front.Rotation = _incomingAngle;

        var throughSize = new Vector2(1700f, 520f * _bodyFit);
        ConfigureQuad(_through, throughSize);
        _through.Rotation = _incomingAngle;
        _through.Visible = _isFinal;

        var fringeSize = new Vector2(820f, DesignBodySize.Y * _bodyFit * 1.25f);
        ConfigureQuad(_fringe, fringeSize);
        _fringe.Rotation = _incomingAngle;
        _fringe.SetInstanceShaderParameter("wall_offset", 0f);
        // 首击沿用震荡的驻波条纹；接力幕若也用粗条纹，会跟"叠了几层"的波前条混淆成同一批亮条。
        _fringe.SetInstanceShaderParameter("spacing", _index > 0 ? 26f + 4f * _index : 74f);
        _fringe.SetInstanceShaderParameter("fade_length", _index > 0 ? 96f + 12f * _index : 190f);
        _fringe.SetInstanceShaderParameter("line_color", FringeColorFor(_index));
        _fringe.SetInstanceShaderParameter("line_sharpness", _index > 0 ? 0.85f : 0.55f);

        _front.SetInstanceShaderParameter("crest_color", FrontColorFor(_index));
        _front.SetInstanceShaderParameter("stack_count", (float)StackCountOf(_index));
        _front.SetInstanceShaderParameter("stack_decay", 0.30f);
        // 边缘要利：彻底软化会读成几根发光的软管，而不是被压出来的压力面。
        _front.SetInstanceShaderParameter("softness", 0.65f);

        _through.SetInstanceShaderParameter("crest_color", FrontColorFor(_index));
        _through.SetInstanceShaderParameter("stack_count", 2f);
        _through.SetInstanceShaderParameter("stack_decay", 0.35f);
        _through.SetInstanceShaderParameter("softness", 0.8f);

        // 回波球：三颗不同分音，尺寸/画布与震荡一致，保证"余音"是同一种东西。
        var launch = new Vector2(0f, -6f);
        var travel = _casterLocal - launch;
        var normal = new Vector2(-travel.Y, travel.X).Normalized();
        for (var i = 0; i < _notes.Count; i++)
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

    /// <summary>摆好一块 shader 画布：尺寸、以中心为轴、同步 quad_size。</summary>
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

        foreach (var note in _notes)
        {
            note.Node.Position = note.P0;
            note.Node.Scale = Vector2.One;
            note.Node.Modulate = Colors.White;
            note.Node.Visible = false;
        }
    }

    private void ApplyState(float progress)
    {
        var duration = _isFinal ? FinalLife : Life;
        var time = Mathf.Clamp(progress, 0f, 1f) * duration;

        ApplyFront(time);
        ApplyFringe(time);
        ApplyCore(time);
        ApplyShards(time);
        ApplyNotes(time);
        ApplyAbsorb(time);
        ApplyThrough(time);
    }

    /// <summary>迎面波前：叠层一起压到表面，压平后主带急颤衰减。越往后到得越快、抖得越狠。</summary>
    private void ApplyFront(float time)
    {
        var arriveEnd = FrontArriveEnd * (1f - 0.07f * _index);
        float offset;
        float thickness;
        float intensity;

        if (time < arriveEnd)
        {
            var local = Mathf.Clamp(time / arriveEnd, 0f, 1f);
            offset = Mathf.Lerp(ArriveFrom, 0f, local * local);
            thickness = Mathf.Lerp(24f + 2f * _index, 10f + 1.2f * _index, local);
            intensity = Mathf.Clamp(local / 0.20f, 0f, 1f);
        }
        else
        {
            var span = FrontSettleEnd - arriveEnd;
            var local = Mathf.Clamp((time - arriveEnd) / span, 0f, 1f);
            var decay = Mathf.Exp(-3.4f * local);
            offset = Mathf.Sin(local * Mathf.Pi * 2f * 3f) * (8f + 3f * _index) * decay;
            // 收尾只留一条急颤的亮线：肿成一块白板会把敌人糊掉。
            thickness = Mathf.Lerp(10f + 1.2f * _index, 8f + _index, local);
            intensity = Mathf.Pow(1f - local, 1.6f) * 0.7f;
        }

        _front.SetInstanceShaderParameter("offset", offset);
        _front.SetInstanceShaderParameter("stack_gap",
            Mathf.Lerp(StackGapStart, StackGapEnd, Mathf.Clamp(time / arriveEnd, 0f, 1f)));
        _front.SetInstanceShaderParameter("thickness", thickness * (_isFinal ? 1.3f : 1f));
        _front.SetInstanceShaderParameter("intensity", intensity * (1f + 0.12f * _index));
        _front.SetInstanceShaderParameter("vertical_fade", 0.34f - 0.04f * _index - (_isFinal ? 0.05f : 0f));
        _front.SetInstanceShaderParameter("bow", Mathf.Lerp(46f, 2f, Mathf.Clamp(time / arriveEnd, 0f, 1f)));
    }

    /// <summary>反射驻波：条纹渐强（与震荡的渐弱相反），末击多留一段。</summary>
    private void ApplyFringe(float time)
    {
        var end = _isFinal ? FinalFringeEnd : FringeEnd;
        var local = Mathf.Clamp((time - FringeStart) / (end - FringeStart), 0f, 1f);
        if (time < FringeStart)
        {
            _fringe.SetInstanceShaderParameter("intensity", 0f);
            return;
        }

        // 渐强包络：越跳越亮，到末尾直接收掉（"最后一响"）。
        var swell = Mathf.Pow(Mathf.Clamp(local / 0.55f, 0f, 1f), 0.7f);
        var cut = 1f - Mathf.Clamp((local - 0.72f) / 0.28f, 0f, 1f);
        var beat = 0.72f + 0.28f * Mathf.Abs(Mathf.Cos(Mathf.Pi * local * FringeBeatCount));
        _fringe.SetInstanceShaderParameter("pulse", beat);
        var fringeGain = _index > 0 ? 0.38f + 0.05f * _index : 0.62f;
        _fringe.SetInstanceShaderParameter("intensity",
            swell * cut * beat * fringeGain * (_isFinal ? 1.25f : 1f));
    }

    /// <summary>接触点高压核：越往后越大越烫。</summary>
    private void ApplyCore(float time)
    {
        var local = Mathf.Clamp(time / 0.12f, 0f, 1f);
        var corePeak = 1.15f + 0.32f * _index + (_isFinal ? 0.3f : 0f);
        _core.Scale = _coreBaseScale * Mathf.Lerp(0.5f, corePeak, EaseOutCubic(local));
        _core.Modulate = new Color(_coreBaseColor.R, _coreBaseColor.G, _coreBaseColor.B,
            _coreBaseColor.A * (1f - local) * (1f - local));
    }

    /// <summary>碎晶：沿节线甩出并受重力，数量与初速随命中次序增长。</summary>
    private void ApplyShards(float time)
    {
        var count = Math.Min(4 + 2 * _index + (_isFinal ? 3 : 0), _shards.Count);
        for (var i = 0; i < _shards.Count; i++)
        {
            var shard = _shards[i];
            var local = Mathf.Clamp((time - ShardStart) / ShardLife, 0f, 1f);
            var visible = i < count && time >= ShardStart && local < 1f;
            shard.Node.Visible = visible;
            if (!visible)
                continue;

            var velocity = ShardVelocity(i);
            var travel = local * ShardLife + 0.035f;
            shard.Node.Position = shard.Start + velocity * travel +
                                  new Vector2(0f, 240f * travel * travel);
            shard.Node.Rotation = velocity.Angle() + Mathf.Pi * 0.25f + shard.Spin * local;
            shard.Node.Scale = shard.BaseScale * Mathf.Lerp(1.25f + 0.1f * _index, 0.5f, local);
            shard.Node.Modulate = new Color(shard.BaseColor.R, shard.BaseColor.G, shard.BaseColor.B,
                shard.BaseColor.A * (1f - local * local));
        }
    }

    /// <summary>回波球：沿弧线飞回演奏者（数量＝该击给与的余音）。</summary>
    private void ApplyNotes(float time)
    {
        for (var i = 0; i < _echoCount && i < _notes.Count; i++)
        {
            var note = _notes[i];
            var local = Mathf.Clamp((time - note.Delay) / NoteFly, 0f, 1f);
            var visible = time >= note.Delay && local < 1f;
            note.Node.Visible = visible;
            if (!visible)
                continue;

            // 先慢后快，读成被吸回。
            var u = Mathf.Lerp(local, EaseInQuad(local), 0.55f);
            var position = QuadraticBezier(note.P0, note.Control, note.P1, u);
            var travelDirection = QuadraticBezier(note.P0, note.Control, note.P1,
                Mathf.Min(u + 0.04f, 1f)) - position;
            position.Y += Mathf.Sin(local * Mathf.Pi * 2f) * 4f;
            note.Node.Position = position;

            var travelUnit = travelDirection.LengthSquared() > 0.001f
                ? travelDirection.Normalized()
                : Vector2.Left;

            note.Node.Rotation = travelUnit.Angle();
            var pop = Mathf.Lerp(0.55f, 1f, Mathf.Clamp(local / 0.22f, 0f, 1f));
            var shrink = Mathf.Lerp(1f, 0.68f, Mathf.Clamp((local - 0.72f) / 0.28f, 0f, 1f));
            var size = pop * shrink;
            note.Node.Scale = new Vector2(size * 1.1f, size * 0.93f);

            var fadeIn = Mathf.Clamp(local / 0.14f, 0f, 1f);
            var fadeOut = 1f - Mathf.Clamp((local - 0.84f) / 0.16f, 0f, 1f);
            note.Node.Modulate = new Color(1f, 1f, 1f, fadeIn * fadeOut);

            note.Orb.SetInstanceShaderParameter("phase", local + i * 0.23f);

            // 拖尾往球后推半个球径，前缘埋进球体，避免在球心画出硬分界。
            note.Trail.PivotOffset = note.Trail.Size * 0.5f;
            note.Trail.Position = new Vector2(-note.Trail.Size.X * 0.5f - OrbSizeFor(i) * 0.16f, 0f)
                                  - note.Trail.Size * 0.5f;
            note.Trail.SetInstanceShaderParameter("strength", 0.78f);
        }
    }

    /// <summary>抵达收束：环从外收束到一点。</summary>
    private void ApplyAbsorb(float time)
    {
        var local = Mathf.Clamp((time - AbsorbStart) / (AbsorbEnd - AbsorbStart), 0f, 1f);
        var visible = time >= AbsorbStart && local < 1f;
        _absorb.Visible = visible;
        if (!visible)
            return;

        var pop = Mathf.Sin(Mathf.Pi * Mathf.Clamp(local * 1.4f, 0f, 1f));
        _absorbRing.Scale = _absorbRingBaseScale * Mathf.Lerp(1.4f, 0.25f, EaseOutCubic(local));
        _absorbRing.Modulate = new Color(_absorbRingBaseColor.R, _absorbRingBaseColor.G,
            _absorbRingBaseColor.B, _absorbRingBaseColor.A * pop);
    }

    /// <summary>末击的穿出波：越过目标继续走，出画后散尽（激荡的波不回家）。</summary>
    private void ApplyThrough(float time)
    {
        if (!_isFinal)
            return;

        var local = Mathf.Clamp((time - ThroughStart) / (ThroughEnd - ThroughStart), 0f, 1f);
        var visible = time >= ThroughStart && local < 1f;
        _through.Visible = visible;
        if (!visible)
            return;

        _through.SetInstanceShaderParameter("offset", Mathf.Lerp(0f, ThroughTravel, EaseInQuad(local)));
        _through.SetInstanceShaderParameter("thickness", Mathf.Lerp(11f, 28f, local));
        _through.SetInstanceShaderParameter("stack_gap", Mathf.Lerp(22f, 38f, local));
        _through.SetInstanceShaderParameter("bow", Mathf.Lerp(10f, 22f, local));
        _through.SetInstanceShaderParameter("intensity",
            Mathf.Sin(Mathf.Pi * Mathf.Pow(local, 0.70f)) * 0.45f);
    }

    private void OnPlaybackFinished()
    {
        if (VfxPreviewSupport.ShouldSelfFree(this))
        {
            this.QueueFreeSafely();
            return;
        }

        if (LoopPreview)
        {
            // 预览时逐拍升阶：一个循环就能看完 1/2/4/8 的递增与末击的穿出。
            _index = (_index + 1) % 4;
            _isFinal = _index >= 3;
            Replay();
        }
    }

    /// <summary>第 index 击的叠层道数：1/2/4/8。</summary>
    private static int StackCountOf(int index) => 1 << Math.Min(index, 3);

    private static Color FringeColorFor(int index)
    {
        var t = Mathf.Clamp(index / 3f, 0f, 1f);
        return new Color(0.82f + 0.10f * t, 0.90f + 0.06f * t, 1f);
    }

    private static Color FrontColorFor(int index)
    {
        var t = Mathf.Clamp(index / 3f, 0f, 1f);
        return new Color(0.86f + 0.12f * t, 0.93f + 0.05f * t, 1f);
    }

    /// <summary>第 i 颗回波球的画布边长（px）。</summary>
    private static float OrbSizeFor(int index) => 131f - index * 13f;

    private Vector2 ShardVelocity(int index)
    {
        var spread = index switch
        {
            0 => -58f,
            1 => 18f,
            2 => -14f,
            3 => 62f,
            4 => 34f,
            5 => -34f,
            6 => -86f,
            7 => 92f,
            8 => 8f,
            9 => -122f,
            10 => 128f,
            11 => -46f,
            _ => 0f,
        };
        var angleDegrees = _incomingAngle * (180f / Mathf.Pi) + spread;
        var speed = 700f + 90f * _index + index * 46f;
        return Vector2.FromAngle(Mathf.DegToRad(angleDegrees)) * speed * Mathf.Lerp(0.8f, 1.1f, index / 11f);
    }

    private static Vector2 QuadraticBezier(Vector2 p0, Vector2 control, Vector2 p1, float u)
    {
        var inverse = 1f - u;
        return inverse * inverse * p0 + 2f * inverse * u * control + u * u * p1;
    }

    private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

    private static float EaseInQuad(float x) => x * x;
}
