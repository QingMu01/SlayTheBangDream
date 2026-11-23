using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using MegaCrit.Sts2.Core.TestSupport;

namespace BangDreamInSpire.Scripts.Nodes.VFX;

/// <summary>
/// 激荡施法/传播：一击回授——<c>2^i</c> 根弦被砸响，<c>2^i</c> 道波前从声源（演奏者或上一个敌人）叠着冲向目标。
/// 纯表现，不参与结算；声学语汇与逐击翻倍的视觉语法见 docs/mechanic-authoring.md 12.4。
/// </summary>
[Tool]
public partial class SurgeWaveVfx : Node2D
{
    private const string ScenePath = "res://BangDreamInSpire/scenes/vfx/surge_wave_vfx.tscn";

    /// <summary>逐击等待（快速 / 普通）：卡牌的 <c>WithWaitBeforeHit</c> 用这两个值，波前飞行时间与它们相等。</summary>
    public const float HitWaitFast = 0.08f;

    public const float HitWaitStandard = 0.12f;

    /// <summary>抵达之后波前继续飞出并淡尽的时长。</summary>
    private const float TailSeconds = 0.17f;

    /// <summary>蓄势时波包压在声源上的半径（px）。</summary>
    private const float StartRadius = 96f;

    /// <summary>弦/波前的最大道数；第 i 击用 <c>2^i</c> 道并夹到上限。</summary>
    private const int MaxLines = 8;

    /// <summary>弦在演奏者身上的锚点；与震荡施法幕的击弦点同一处，两幕看起来是同一件乐器。</summary>
    private static readonly Vector2 ChordAnchorOffset = new(126f, -74f);

    private const float StringSpacing = 15f;

    /// <summary>弦从被砸响到静下来的时间。</summary>
    private const float StringSettleSeconds = 0.18f;

    /// <summary>弦被砸响瞬间的横向摆幅（px）。</summary>
    private const float StringJitter = 13f;

    /// <summary>波前间距上下限：接力时两敌之间只有 ~70px，间距得按距离自己收，否则前列叠成一根横杠。</summary>
    private const float FrontGapMin = 20f;

    private const float FrontGapMax = 56f;

    /// <summary>整列波前占这段航程的比例：留出 30% 的余量给尾迹与目标体型。</summary>
    private const float FrontSpreadRatio = 0.70f;

    private static readonly Vector2 PreviewCasterOffset = new(-620f, 30f);

    private const float PreviewDistance = 900f;

    /// <summary>预览里"上一个敌人"相对弦的偏移：接力幕的声源不能和弦重合。</summary>
    private static readonly Vector2 PreviewRelayOffset = new(540f, 10f);

    private const float PreviewAnimDelay = 0.15f;

    private readonly List<Sprite2D> _strings = [];

    private ColorRect _packet = null!;
    private Node2D _chord = null!;
    private Sprite2D _core = null!;
    private Node2D _pickup = null!;
    private Sprite2D _pickupRing = null!;
    private Node2D _pickupStripes = null!;

    private Vector2 _stringBaseScale = Vector2.One;
    private Color _stringBaseColor = Colors.White;
    private Vector2 _coreBaseScale = Vector2.One;
    private Color _coreBaseColor = Colors.White;
    private Vector2 _ringBaseScale = Vector2.One;
    private Color _ringBaseColor = Colors.White;
    private Vector2 _stripeBaseScale = Vector2.One;
    private Color _stripeBaseColor = Colors.White;

    /// <summary>声源（世界坐标）：首击是演奏者，之后是上一个敌人。</summary>
    private Vector2 _origin;

    /// <summary>目标中心（世界坐标）。</summary>
    private Vector2 _target;

    /// <summary>弦的世界坐标。</summary>
    private Vector2 _chordPoint;

    private float _distance = PreviewDistance;

    private float _aimAngle;

    /// <summary>本场是第几击（0 起）：决定弦数、波前道数与色温。</summary>
    private int _index;

    /// <summary>是否由上一个敌人弹出（而非从演奏者起手）。</summary>
    private bool _relay;

    /// <summary>是否整张牌的最后一击：末击的弦要放得更狠。</summary>
    private bool _isFinal;

    /// <summary>蓄势时长＝攻击动画的等待；飞行时长＝逐击等待。两段合成施法幕。</summary>
    private float _charge;

    private float _flight = HitWaitStandard;

    private bool _configured;

    private Tween? _tween;

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
            _isFinal = _index >= 3;
            if (VfxPreviewSupport.IsPreviewRun(this))
                Replay();
        }
    }

    /// <summary>波前飞行时间：卡牌的逐击等待与此相等，命中幕就落在波前抵达那一刻。</summary>
    public static float TravelSeconds =>
        IsInstantMode ? 0.05f : IsFastMode ? HitWaitFast : HitWaitStandard;

    private static FastModeType? CurrentFastMode
    {
        get
        {
            // 单独预览 / 编辑器里没有存档管理器，按普通模式取时长。
            var saveManager = SaveManager.Instance;
            return saveManager?.PrefsSave?.FastMode;
        }
    }

    private static bool IsFastMode => CurrentFastMode == FastModeType.Fast;

    /// <summary>瞬时模式不等待任何动画：两段时长一起压到最短，至少别让三击的三道理拖在结算之后飞。</summary>
    private static bool IsInstantMode => CurrentFastMode == FastModeType.Instant;

    /// <summary>攻击动画的等待：与 <c>CreatureCmd.TriggerAnim</c> 同一口径（快模式取一半、封顶 0.25s）。</summary>
    public static float AttackAnimWait(float attackAnimDelay) => IsInstantMode
        ? 0.05f
        : IsFastMode
            ? Mathf.Min(attackAnimDelay * 0.5f, 0.25f)
            : attackAnimDelay;

    /// <summary>创建一招的回授波；previousTarget 为空表示这一击从演奏者起手。</summary>
    public static SurgeWaveVfx? Create(
        Creature caster, Creature? previousTarget, Creature target, int index, int totalHits, float attackAnimDelay)
    {
        if (TestMode.IsOn)
            return null;

        var room = NCombatRoom.Instance;
        if (room?.GetCreatureNode(caster) is not { } casterNode ||
            room.GetCreatureNode(target) is not { } targetNode)
            return null;

        var casterPoint = casterNode.VfxSpawnPosition;
        var targetPoint = targetNode.VfxSpawnPosition;
        var chordPoint = casterPoint + ChordAnchorOffset;

        Vector2 origin;
        if (index <= 0 || previousTarget == null || room.GetCreatureNode(previousTarget) is not { } previousNode)
        {
            origin = chordPoint;
        }
        else
        {
            // 声源落在上一个敌人朝着目标的一侧：波是「从它身上弹出去」的，不是凭空出现。
            var previousPoint = previousNode.VfxSpawnPosition;
            var direction = (targetPoint - previousPoint).Normalized();
            origin = previousPoint + direction * (previousNode.Hitbox.Size.X * 0.30f);
        }

        var vfx = BangDreamPreloadManager.GetScene(ScenePath).Instantiate<SurgeWaveVfx>();
        vfx.Configure(origin, targetPoint, chordPoint, index, totalHits,
            AttackAnimWait(attackAnimDelay), TravelSeconds);
        return vfx;
    }

    /// <summary>合成预览用接缝：手工指定声源、目标、弦的世界坐标与两段时长。</summary>
    public void Configure(
        Vector2 origin, Vector2 target, Vector2 chordPoint, int index, int totalHits, float charge, float flight)
    {
        _origin = origin;
        _target = target;
        _chordPoint = chordPoint;
        _index = Mathf.Max(0, index);
        _isFinal = _index >= Mathf.Max(totalHits - 1, 0);
        _charge = Mathf.Max(0f, charge);
        _flight = Mathf.Max(0.02f, flight);
        _configured = true;
        if (IsNodeReady())
            Replay();
    }

    public override void _Ready()
    {
        _packet = GetNode<ColorRect>("Packet");
        _chord = GetNode<Node2D>("Chord");
        _core = GetNode<Sprite2D>("Chord/Core");
        _pickup = GetNode<Node2D>("Pickup");
        _pickupRing = GetNode<Sprite2D>("Pickup/Ring");
        _pickupStripes = GetNode<Node2D>("Pickup/Stripes");

        for (var i = 0; i < MaxLines; i++)
        {
            if (GetNodeOrNull<Sprite2D>($"Chord/Strings/String{i}") is { } line)
                _strings.Add(line);
        }

        if (_strings.Count > 0)
        {
            _stringBaseScale = _strings[0].Scale;
            _stringBaseColor = _strings[0].Modulate;
        }

        _coreBaseScale = _core.Scale;
        _coreBaseColor = _core.Modulate;
        _ringBaseScale = _pickupRing.Scale;
        _ringBaseColor = _pickupRing.Modulate;
        if (GetNodeOrNull<Sprite2D>("Pickup/Stripes/Stripe0") is { } stripe)
        {
            _stripeBaseScale = stripe.Scale;
            _stripeBaseColor = stripe.Modulate;
        }

        if (VfxPreviewSupport.AutoPlayOnReady)
            Replay();
    }

    public void Replay()
    {
        _tween?.Kill();

        if (!_configured)
            SetUpPreview();

        _relay = _index > 0;
        GlobalPosition = _origin;

        var delta = _target - _origin;
        _distance = Mathf.Clamp(delta.Length(), 160f, 1400f);
        _aimAngle = delta.Angle();

        ApplyPacketGeometry();
        ApplyChordGeometry();
        _pickup.Position = Vector2.Zero;
        _pickup.Visible = _relay;

        _tween = CreateTween();
        _tween.TweenMethod(Callable.From<float>(ApplyState), 0f, 1f, _charge + _flight + TailSeconds);
        _tween.Finished += OnPlaybackFinished;
    }

    /// <summary>预览摆位：演奏者在左，接力幕的声源放在"上一个敌人"处。</summary>
    private void SetUpPreview()
    {
        VfxPreviewSupport.CenterForPreview(this);
        _chordPoint = GlobalPosition + PreviewCasterOffset;
        _origin = _index > 0 ? _chordPoint + PreviewRelayOffset : _chordPoint;
        _target = _chordPoint + new Vector2(PreviewDistance, -40f);
        _charge = AttackAnimWait(PreviewAnimDelay);
        _flight = TravelSeconds;
        _isFinal = _index >= 3;
    }

    /// <summary>波包画布：以声源为圆心，盖住全程半径与纵向条带。</summary>
    private void ApplyPacketGeometry()
    {
        var half = _distance + 520f;
        var size = new Vector2(half * 2f, half * 2f);
        _packet.Position = -half * Vector2.One;
        _packet.Size = size;
        _packet.SetInstanceShaderParameter("quad_size", size);
        _packet.SetInstanceShaderParameter("angle_center", _aimAngle);
        _packet.SetInstanceShaderParameter("crest_color", CrestColorFor(_index));
        _packet.SetInstanceShaderParameter("front_color", FrontColorFor(_index));
        // 墙体高度压到敌人身高附近：比两个敌人之间的空隙还高会读成一整片罩子，也会盖住头顶的意图图标。
        _packet.SetInstanceShaderParameter("band_half_height", _relay ? 200f : 240f);
        _packet.SetInstanceShaderParameter("flatness", FlatnessCharge);
        _packet.SetInstanceShaderParameter("bow", 0f);
    }

    /// <summary>弦列：本击的 <c>2^i</c> 根弦居中铺开，越靠外越暗。</summary>
    private void ApplyChordGeometry()
    {
        _chord.Position = _chordPoint - _origin;
        _chord.Rotation = 0f;

        var count = StringCountOf(_index);
        for (var i = 0; i < _strings.Count; i++)
        {
            var line = _strings[i];
            line.Visible = i < count;
            if (!line.Visible)
                continue;

            line.Position = ChordSlotOffset(i, count);
            // 靠外的弦稍短：一列弦看起来像乐器，而不是一排栅栏。
            line.Scale = new Vector2(_stringBaseScale.X,
                _stringBaseScale.Y * (1f - 0.06f * Mathf.Abs(i - (count - 1) * 0.5f)));
            line.Modulate = _stringBaseColor;
        }

        _core.Position = Vector2.Zero;
        _core.Scale = _coreBaseScale;
        _core.Modulate = _coreBaseColor;

        foreach (var stripe in _pickupStripes.GetChildren())
        {
            if (stripe is Sprite2D sprite)
            {
                sprite.Scale = _stripeBaseScale;
                sprite.Modulate = _stripeBaseColor;
            }
        }

        _pickupRing.Scale = _ringBaseScale;
        _pickupRing.Modulate = _ringBaseColor;
        _pickupRing.Rotation = 0f;
    }

    private void ApplyState(float progress)
    {
        var time = Mathf.Clamp(progress, 0f, 1f) * (_charge + _flight + TailSeconds);

        ApplyPacket(time);
        ApplyChord(time);
        ApplyPickup(time);
    }

    /// <summary>波包：先在声源蓄势收束，再在飞行段展开成 <c>2^i</c> 道前列冲出。</summary>
    private void ApplyPacket(float time)
    {
        var count = LineCountOf(_index);
        var packed = StartRadius / (count + 0.8f);
        var spread = count <= 1
            ? FrontGapMax
            : Mathf.Clamp(_distance * FrontSpreadRatio / (count - 1), FrontGapMin, FrontGapMax);
        float radius;
        float gap;
        float intensity;

        var charge = Mathf.Max(_charge, 0.0001f);
        if (time < _charge)
        {
            var local = time / charge;
            radius = Mathf.Lerp(StartRadius * 0.35f, StartRadius, EaseOutCubic(local));
            gap = packed;
            intensity = Mathf.Lerp(0.30f, 1f, local);
        }
        else if (time < _charge + _flight)
        {
            var local = (time - _charge) / _flight;
            radius = Mathf.Lerp(StartRadius, _distance, local);
            // 一出手就散开：飞行只有 ~0.12s（7 帧），慢慢摊开的话道数根本来不及被数出来。
            gap = Mathf.Lerp(packed, spread, Mathf.Clamp(local * 3.2f, 0f, 1f));
            intensity = 1f;
        }
        else
        {
            var local = Mathf.Clamp((time - _charge - _flight) / TailSeconds, 0f, 1f);
            radius = _distance + _distance * 0.28f * local;
            gap = spread;
            intensity = Mathf.Pow(1f - local, 1.25f);
        }

        // 形状随出手过渡：蓄势是一个点上的鼓包，出手后拉直成一道墙。
        var shape = Mathf.Clamp((time - _charge) / Mathf.Max(_flight, 0.0001f), 0f, 1f);
        shape = EaseOutCubic(shape);

        _packet.SetInstanceShaderParameter("front_radius", radius);
        _packet.SetInstanceShaderParameter("front_count", (float)count);
        _packet.SetInstanceShaderParameter("front_gap", gap);
        _packet.SetInstanceShaderParameter("flatness", Mathf.Lerp(FlatnessCharge, FlatnessTarget, shape));
        _packet.SetInstanceShaderParameter("bow", Mathf.Lerp(0f, BowTarget, shape));
        // 前列越厚，尾迹越该退场：衰减是震荡的语汇，激荡只剩"越来越厚的压力"。
        _packet.SetInstanceShaderParameter("crest_strength",
            Mathf.Clamp(1f - 0.20f * _index, 0.35f, 1f) * Mathf.Clamp(time / Mathf.Max(_charge, 0.02f), 0f, 1f));
        _packet.SetInstanceShaderParameter("intensity",
            intensity * IntensityFor(_index) * (_isFinal ? 1.15f : 1f));
    }

    /// <summary>弦：被砸响后横摆衰减；后几击只是"续拍"，比首击暗。</summary>
    private void ApplyChord(float time)
    {
        var settle = Mathf.Clamp(time / StringSettleSeconds, 0f, 1f);
        var bloom = 1f - settle;
        // 末击是整张牌的收束：接力幕里本来压暗的弦在这里重新炸开。
        var beatScale = _isFinal ? 1.25f : _relay ? 0.55f : 1f;
        var count = StringCountOf(_index);
        // 弦多了总亮度不能跟着翻倍，否则最后一下会把演奏者糊成一块白。
        var countAlpha = Mathf.Lerp(1f, 0.58f, (count - 1) / 7f);

        for (var i = 0; i < _strings.Count; i++)
        {
            if (i >= count)
                continue;

            var line = _strings[i];
            var sign = i % 2 == 0 ? 1f : -1f;
            var swing = Mathf.Sin(time * 52f + i * 0.7f) * StringJitter * bloom * bloom;
            line.Position = ChordSlotOffset(i, count) + new Vector2(sign * swing, 0f);

            // 击弦瞬间最亮，随后整列弦一起暗下去（不写死颜色，保留场景基准色相）。
            var alpha = _stringBaseColor.A * beatScale * countAlpha * Mathf.Pow(bloom, 1.35f) *
                        (0.70f + 0.30f * Mathf.Cos(i * 0.5f));
            line.Modulate = new Color(_stringBaseColor.R, _stringBaseColor.G, _stringBaseColor.B,
                Mathf.Clamp(alpha, 0f, 1f));
            line.Scale = new Vector2(_stringBaseScale.X * (1f + 0.5f * bloom * bloom), line.Scale.Y);
        }

        var coreT = Mathf.Clamp(time / 0.11f, 0f, 1f);
        var corePeak = 1.35f + 0.12f * _index + (_isFinal ? 0.35f : 0f);
        _core.Scale = _coreBaseScale * Mathf.Lerp(0.45f, corePeak, EaseOutCubic(coreT));
        _core.Modulate = new Color(_coreBaseColor.R, _coreBaseColor.G, _coreBaseColor.B,
            _coreBaseColor.A * beatScale * (1f - coreT) * (1f - coreT));
    }

    /// <summary>回授：蓄势段里上一击的残响被抽进离体的波中。</summary>
    private void ApplyPickup(float time)
    {
        if (!_relay)
            return;

        var span = Mathf.Max(_charge, 0.06f);
        var local = Mathf.Clamp(time / span, 0f, 1f);
        _pickup.Visible = local < 1f;
        if (!_pickup.Visible)
            return;

        var swell = Mathf.Sin(Mathf.Pi * Mathf.Min(local * 1.15f, 1f));
        _pickupRing.Scale = _ringBaseScale * Mathf.Lerp(2.4f, 0.30f, EaseOutCubic(local));
        _pickupRing.Modulate = new Color(_ringBaseColor.R, _ringBaseColor.G, _ringBaseColor.B,
            _ringBaseColor.A * swell * 0.85f);
        _pickupRing.Rotation = local * 1.2f;

        // 上一击留在敌人身上的驻波条纹被抽走：条纹与来波方向垂直，朝声源收拢。
        var stripes = _pickupStripes.GetChildren();
        for (var i = 0; i < stripes.Count; i++)
        {
            if (stripes[i] is not Sprite2D stripe)
                continue;

            var back = -Mathf.Lerp(40f, 190f, i / Mathf.Max(stripes.Count - 1f, 1f));
            var pull = EaseOutCubic(local);
            stripe.Position = new Vector2(back * (1f - pull), 0f);
            stripe.Rotation = _aimAngle;
            stripe.Scale = _stripeBaseScale * Mathf.Lerp(1f, 0.35f, local);
            stripe.Modulate = new Color(_stripeBaseColor.R, _stripeBaseColor.G, _stripeBaseColor.B,
                _stripeBaseColor.A * swell * (1f - i * 0.16f));
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
        {
            // 预览时逐拍升一阶：一个循环就能看完 1/2/4/8 的递增。
            _index = (_index + 1) % 4;
            _isFinal = _index >= 3;
            Replay();
        }
    }

    /// <summary>出手后的波前形状：蓄势时是点声源（球面弧），出手后拉直成墙；接力幕拉得更彻底。</summary>
    private float FlatnessTarget => _relay ? 1f : 0.45f;

    /// <summary>接力幕的声源紧贴着上一个敌人，一开始就该是平面波；首击才从一个点发散。</summary>
    private float FlatnessCharge => _relay ? 0.62f : 0f;

    private float BowTarget => _relay ? 14f : 24f;

    /// <summary>波前道数（＝这一击翻了几倍）：1/2/4/8。</summary>
    private static int LineCountOf(int index) => 1 << Math.Min(index, 3);

    /// <summary>弦数＝已经击过的次数（i+1）：每击多一根还在响的弦，这是演奏者的动作，不是翻倍本身。</summary>
    private static int StringCountOf(int index) => Math.Min(index + 1, MaxLines);

    /// <summary>第 i 个槽位的横向位置：整列按当前道数居中。</summary>
    private static Vector2 ChordSlotOffset(int slot, int count) =>
        new((slot - (count - 1) * 0.5f) * StringSpacing, 0f);

    /// <summary>越往后越白：高一阶分音听起来更亮，颜色只做陪衬。</summary>
    private static float IntensityFor(int index) => 1f + 0.10f * Math.Min(index, 3);

    private static Color CrestColorFor(int index)
    {
        var t = Mathf.Clamp(index / 3f, 0f, 1f);
        return new Color(0.55f + 0.25f * t, 0.72f + 0.14f * t, 1f);
    }

    private static Color FrontColorFor(int index)
    {
        var t = Mathf.Clamp(index / 3f, 0f, 1f);
        return new Color(1f, 0.98f + 0.02f * t, 0.96f + 0.04f * t);
    }

    private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
}
