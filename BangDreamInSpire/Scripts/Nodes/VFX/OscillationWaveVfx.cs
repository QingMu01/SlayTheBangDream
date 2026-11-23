using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace BangDreamInSpire.Scripts.Nodes.VFX;

/// <summary>
/// 震荡施法：击弦点抖出一列疏密相间的压力波包，朝敌方展开。纯表现，不参与结算。
/// 声学语汇见 docs/mechanic-authoring.md「震荡」章节：声音是压缩与稀疏的交替，
/// 且高次分音先死 —— 所以波包边跑边由「尖」变「软」，不是匀速放大的光环。
/// </summary>
[Tool]
public partial class OscillationWaveVfx : Node2D
{
    private const string ScenePath = "res://BangDreamInSpire/scenes/vfx/oscillation_wave_vfx.tscn";

    /// <summary>
    /// 波前从施法者抵达敌阵的用时。卡牌的 <c>WithWaitBeforeHit</c> 必须对齐这个值，
    /// 伤害才落在「波压到目标身上」的那一刻，而不是先掉血后看见波。
    /// </summary>
    public const float FrontTravelSeconds = 0.15f;

    /// <summary>波包淡出完成的时刻。</summary>
    private const float Life = 0.34f;

    /// <summary>起振：弦从「还在抖」收成一条实线。</summary>
    private const float StringSettleSeconds = 0.09f;

    private const float StringFadeSeconds = 0.16f;

    /// <summary>波前起始半径（px）：从弦的位置就开始算，线在这里变成弧。</summary>
    private const float StartRadius = 34f;

    /// <summary>各道压缩峰的相对振幅：传播中后峰先死（高次分音先衰减）。</summary>
    private static readonly Vector4 CrestAmplitudeStart = new(0.72f, 0.46f, 0.24f, 0f);

    private static readonly Vector4 CrestAmplitudeEnd = new(0.40f, 0.10f, 0f, 0f);

    /// <summary>各道压缩峰与前缘的间距：传播中拉开，波列被「拉长」。</summary>
    private static readonly Vector4 CrestSpacingStart = new(62f, 132f, 216f, 320f);

    private static readonly Vector4 CrestSpacingEnd = new(158f, 352f, 570f, 320f);

    /// <summary>预览态：施法者摆位与波包最大半径。</summary>
    private static readonly Vector2 PreviewCasterOffset = new(-620f, 30f);

    private const float PreviewMaxRadius = 700f;

    private ColorRect _packet = null!;
    private Sprite2D _string = null!;
    private Sprite2D _stringNear = null!;
    private Sprite2D _stringFar = null!;
    private Sprite2D _core = null!;

    private Vector2 _stringBasePosition;
    private Vector2 _stringBaseScale;
    private Color _stringBaseColor = Colors.White;
    private Vector2 _coreBaseScale;
    private Color _coreBaseColor = Colors.White;
    private Color _nearBaseColor = Colors.White;
    private Color _farBaseColor = Colors.White;
    private Vector2 _nearBasePosition;
    private Vector2 _farBasePosition;

    /// <summary>入树后应用的施法者世界坐标。</summary>
    private Vector2 _spawnWorld;

    /// <summary>波包最大半径：由最远敌人决定。</summary>
    private float _maxRadius = PreviewMaxRadius;

    /// <summary>波前主方向（弧度）：朝向敌阵重心。</summary>
    private float _aimAngle;

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

    /// <summary>创建施法表现；targets 用于决定波包半径与展开方向。</summary>
    public static OscillationWaveVfx? Create(Creature caster, IReadOnlyCollection<Creature> targets)
    {
        if (TestMode.IsOn)
            return null;

        if (NCombatRoom.Instance?.GetCreatureNode(caster) is not { } casterNode)
            return null;

        var vfx = BangDreamPreloadManager.GetScene(ScenePath).Instantiate<OscillationWaveVfx>();
        var origin = casterNode.VfxSpawnPosition;
        vfx._spawnWorld = origin;

        // 半径取最远敌人的横向距离：波前正好扫过整条敌阵。
        var farthest = 0f;
        var centroid = Vector2.Zero;
        var count = 0;
        foreach (var target in targets)
        {
            if (NCombatRoom.Instance.GetCreatureNode(target) is not { } targetNode)
                continue;

            var delta = targetNode.VfxSpawnPosition - origin;
            farthest = Mathf.Max(farthest, Mathf.Abs(delta.X));
            centroid += targetNode.VfxSpawnPosition;
            count++;
        }

        vfx._maxRadius = Mathf.Clamp(farthest + 90f, 260f, 1150f);
        vfx._aimAngle = count > 0
            ? (centroid / count - origin).Angle()
            : 0f;
        return vfx;
    }

    public override void _Ready()
    {
        _packet = GetNode<ColorRect>("Packet");
        _string = GetNode<Sprite2D>("Source/String");
        _stringNear = GetNode<Sprite2D>("Source/StringNear");
        _stringFar = GetNode<Sprite2D>("Source/StringFar");
        _core = GetNode<Sprite2D>("Source/Core");

        // 只读美术基准，编辑器里不改变视觉状态。
        _stringBasePosition = _string.Position;
        _stringBaseScale = _string.Scale;
        _stringBaseColor = _string.Modulate;
        _coreBaseScale = _core.Scale;
        _coreBaseColor = _core.Modulate;
        _nearBaseColor = _stringNear.Modulate;
        _farBaseColor = _stringFar.Modulate;
        _nearBasePosition = _stringNear.Position;
        _farBasePosition = _stringFar.Position;

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

        // 预览态没有真实敌阵：自己造一个「演奏者在左、敌人在右」的摆位，
        // 波包半径与方向按它算，F6 看到的才是实战比例。
        if (VfxPreviewSupport.IsPreviewRun(this))
        {
            Position += PreviewCasterOffset;
            _maxRadius = PreviewMaxRadius;
            _aimAngle = 0f;
        }

        var half = _maxRadius * 1.16f;
        var size = new Vector2(half * 2f, half * 2f);
        _packet.Position = -half * Vector2.One;
        _packet.Size = size;
        _packet.SetInstanceShaderParameter("quad_size", size);
        _packet.SetInstanceShaderParameter("angle_center", _aimAngle);
        _packet.SetInstanceShaderParameter("angle_half", 0.86f);
        _packet.SetInstanceShaderParameter("band_half_height", 340f);

        _string.Position = _stringBasePosition;
        _string.Scale = _stringBaseScale;
        _string.Modulate = _stringBaseColor;
        _stringNear.Position = _nearBasePosition;
        _stringNear.Modulate = _nearBaseColor;
        _stringFar.Position = _farBasePosition;
        _stringFar.Modulate = _farBaseColor;
        _core.Scale = _coreBaseScale;
        _core.Modulate = _coreBaseColor;

        // 所有状态由单一 TweenMethod 驱动。
        _tween = CreateTween();
        _tween.TweenMethod(Callable.From<float>(ApplyState), 0f, 1f, Life);
        _tween.Finished += OnPlaybackFinished;
    }

    private void ApplyState(float progress)
    {
        var time = Mathf.Clamp(progress, 0f, 1f) * Life;

        ApplyPacket(time);
        ApplySource(time);
    }

    /// <summary>波包：半径匀速（声速恒定），谐波与包络随时间变化。</summary>
    private void ApplyPacket(float time)
    {
        var speed = (_maxRadius - StartRadius) / FrontTravelSeconds;
        // 前缘停在上限就不再前进；此时强度已接近 0，停住看不见。
        var radius = Mathf.Min(StartRadius + speed * time, _maxRadius * 1.14f);
        _packet.SetInstanceShaderParameter("front_radius", radius);

        // 传播进度：0 = 刚离开弦，1 = 抵达并消散。
        var travel = Mathf.Clamp(time / Life, 0f, 1f);

        // 谐波衰减：后峰先消失、各峰变宽、间距拉开 —— 同一件事的三个侧面。
        var decay = Mathf.Clamp(travel / 0.78f, 0f, 1f);
        _packet.SetInstanceShaderParameter("crest_amplitude", new Vector4(
            Mathf.Lerp(CrestAmplitudeStart.X, CrestAmplitudeEnd.X, decay),
            Mathf.Lerp(CrestAmplitudeStart.Y, CrestAmplitudeEnd.Y, decay),
            Mathf.Lerp(CrestAmplitudeStart.Z, CrestAmplitudeEnd.Z, decay),
            0f));
        _packet.SetInstanceShaderParameter("crest_spacing", new Vector4(
            Mathf.Lerp(CrestSpacingStart.X, CrestSpacingEnd.X, decay),
            Mathf.Lerp(CrestSpacingStart.Y, CrestSpacingEnd.Y, decay),
            Mathf.Lerp(CrestSpacingStart.Z, CrestSpacingEnd.Z, decay),
            CrestSpacingStart.W));
        _packet.SetInstanceShaderParameter("crest_softness", Mathf.Lerp(0.72f, 1.55f, decay));

        // 起振 0.02s，抵达敌阵前后淡出。
        var rise = Mathf.Clamp(time / 0.02f, 0f, 1f);
        var fade = time <= FrontTravelSeconds + 0.02f
            ? 1f
            : 1f - Mathf.Clamp(
                (time - FrontTravelSeconds - 0.02f) / (Life - FrontTravelSeconds - 0.02f), 0f, 1f);
        _packet.SetInstanceShaderParameter("intensity", rise * Mathf.Pow(fade, 1.35f));
    }

    /// <summary>声源：被击响的弦由「还在抖」收成一条实线，同时闪一下击弦核。</summary>
    private void ApplySource(float time)
    {
        var settle = Mathf.Clamp(time / StringSettleSeconds, 0f, 1f);
        var fade = time <= StringSettleSeconds
            ? 1f
            : 1f - Mathf.Clamp((time - StringSettleSeconds) / (StringFadeSeconds - StringSettleSeconds),
                0f, 1f);

        // 抖动余量：从 ±15px 收到 0，宽度同步收敛 —— 读成余振衰减。
        var blur = (1f - settle) * 15f;
        var spread = Mathf.Lerp(2.4f, 1f, settle);

        _string.Scale = new Vector2(_stringBaseScale.X * spread, _stringBaseScale.Y);
        _string.Modulate = new Color(_stringBaseColor.R, _stringBaseColor.G, _stringBaseColor.B,
            Mathf.Lerp(0.82f, 1f, settle) * fade);

        _stringNear.Position = _nearBasePosition + new Vector2(blur * 0.62f, 0f);
        _stringNear.Modulate = new Color(_nearBaseColor.R, _nearBaseColor.G, _nearBaseColor.B,
            _nearBaseColor.A * 0.55f * (1f - settle) * fade);

        _stringFar.Position = _farBasePosition - new Vector2(blur, 0f);
        _stringFar.Modulate = new Color(_farBaseColor.R, _farBaseColor.G, _farBaseColor.B,
            _farBaseColor.A * 0.30f * (1f - settle) * fade);

        var coreT = Mathf.Clamp(time / 0.09f, 0f, 1f);
        _core.Scale = _coreBaseScale * Mathf.Lerp(0.35f, 1.25f, EaseOutCubic(coreT));
        _core.Modulate = new Color(_coreBaseColor.R, _coreBaseColor.G, _coreBaseColor.B,
            _coreBaseColor.A * (1f - coreT) * (1f - coreT));
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
