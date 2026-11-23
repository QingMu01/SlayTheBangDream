using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace ItsCrychic.Scripts.Nodes.VFX;

/// <summary>白芯蓝紫边斩击与星辉，纯表现，不参与结算。</summary>
[Tool]
public partial class StrikeSakikoVfx : Node2D
{
    private const string ScenePath = "res://ItsCrychic/scenes/vfx/strike_sakiko_vfx.tscn";

    private const float LifeTime = 0.40f;

    private const float FrameDuration = 1f / 15f;

    private const int SlashFrameCount = 4;
    private const float SlashEnd = FrameDuration * SlashFrameCount;
    private const float SlashFadeStart = 0.233f;

    private const float GlowStart = 0.067f;
    private const float GlowRiseTime = 0.020f;
    private const float GlowPeakAlpha = 0.85f;
    private const float GlowFadeStart = 0.267f;
    private const float GlowScaleGrowth = 1.07f;

    private const float BigStarStart = 0.067f;
    private const float BigStarRiseTime = 0.013f;
    private const float BigStarHoldEnd = 0.130f;
    private const float BigStarEnd = 0.240f;
    private const float BigStarPopScale = 0.58f;

    private const float SmallStarStart = 0.072f;
    private const float SmallStarFadeStart = 0.200f;
    private const float SmallStarPopScale = 0.6f;

    /// <summary>入树后才应用创建时的世界坐标。</summary>
    private Vector2 _spawnWorld;

    /// <summary>星尘角度与距离，顺序对应 SmallStar0..5。</summary>
    private static readonly (float AngleDegrees, float Distance)[] SmallStarConfigs =
    [
        (-150f, 118f),
        (-100f, 142f),
        (-45f, 128f),
        (15f, 150f),
        (70f, 136f),
        (135f, 144f),
    ];

    private Sprite2D _glow = null!;
    private Sprite2D _slash = null!;
    private Sprite2D _bigStar = null!;
    private Sprite2D[] _smallStars = [];

    /// <summary>运行时加载的本体斩击帧。</summary>
    [Export] private Texture2D[] _frames = [];

    private const string SlashTexturePathFormat =
        "res://images/vfx/vfx_attack_slash/vfx_attack_slash_0{0}.png";

    private const string BigStarTexturePath = "res://images/packed/vfx/generic/sparkle.png";
    private const string SmallStarTexturePath = "res://images/packed/vfx/generic/sparkle_2.png";

    // 从场景读回的美术基准值，重播时按此复位（不写死初值）。
    private Vector2 _glowBaseScale;
    private Color _glowBaseColor;
    private Color _bigStarBaseColor;
    private Vector2 _bigStarBaseScale;
    private Vector2[] _smallStarBaseScales = [];
    private Color[] _smallStarBaseColors = [];
    private float _rootBaseRotation;
    private float _randomRotationMaxDegrees = 25f;

    private Tween? _tween;

    [Export] public bool RandomizeRotation { get; set; } = true;

    [Export]
    public float RandomRotationMaxDegrees
    {
        get => _randomRotationMaxDegrees;
        set
        {
            _randomRotationMaxDegrees = Mathf.Max(0f, value);
            if (IsInsideTree() && VfxPreviewSupport.IsPreviewRun(this))
                Replay();
        }
    }

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

    public static StrikeSakikoVfx? Create(Creature target)
    {
        if (TestMode.IsOn)
            return null;

        if (NCombatRoom.Instance?.GetCreatureNode(target) is not { } creatureNode)
            return null;

        var vfx = BangDreamPreloadManager.GetScene(ScenePath).Instantiate<StrikeSakikoVfx>();
        vfx._spawnWorld = creatureNode.VfxSpawnPosition;
        return vfx;
    }

    public override void _Ready()
    {
        GlobalPosition = _spawnWorld;
        _glow = GetNode<Sprite2D>("Glow");
        _slash = GetNode<Sprite2D>("Slash");
        _bigStar = GetNode<Sprite2D>("BigStar");

        _smallStars =
        [
            GetNode<Sprite2D>("Stars/SmallStar0"),
            GetNode<Sprite2D>("Stars/SmallStar1"),
            GetNode<Sprite2D>("Stars/SmallStar2"),
            GetNode<Sprite2D>("Stars/SmallStar3"),
            GetNode<Sprite2D>("Stars/SmallStar4"),
            GetNode<Sprite2D>("Stars/SmallStar5"),
        ];

        EnsureDefaultTextures();

        // 只读美术基准，编辑器中不改变视觉状态。
        _glowBaseScale = _glow.Scale;
        _glowBaseColor = _glow.Modulate;
        _bigStarBaseColor = _bigStar.Modulate;
        _bigStarBaseScale = _bigStar.Scale;
        _rootBaseRotation = Rotation;
        _smallStarBaseScales = _smallStars.Select(star => star.Scale).ToArray();
        _smallStarBaseColors = _smallStars.Select(star => star.Modulate).ToArray();

        if (_frames.Length != SlashFrameCount)
            GD.PushError($"[StrikeSakikoVfx] Frames 应为 {SlashFrameCount} 张，实际 {_frames.Length} 张。");

        if (VfxPreviewSupport.AutoPlayOnReady)
            Replay();
    }

    /// <summary>
    /// 本体贴图在运行时按场景中的对应关系加载，避免内容工程引用缺失的 `res://images/**` 资源。
    /// </summary>
    private void EnsureDefaultTextures()
    {
        if (_frames.Length != SlashFrameCount)
        {
            _frames = new Texture2D[SlashFrameCount];
            for (var i = 0; i < SlashFrameCount; i++)
            {
                _frames[i] = BangDreamPreloadManager.GetTexture2D(
                    string.Format(SlashTexturePathFormat, i));
            }
        }

        _glow.Texture ??= _frames[1];
        _slash.Texture ??= _frames[0];
        _bigStar.Texture ??= BangDreamPreloadManager.GetTexture2D(BigStarTexturePath);
        foreach (var smallStar in _smallStars)
        {
            smallStar.Texture ??= BangDreamPreloadManager.GetTexture2D(SmallStarTexturePath);
        }
    }

    public void Replay()
    {
        _tween?.Kill();

        // 预览居中；游戏内位置由调用方决定。
        VfxPreviewSupport.CenterForPreview(this);

        Rotation = _rootBaseRotation;
        if (RandomizeRotation)
            Rotation += Mathf.DegToRad((float)GD.RandRange(0.0, RandomRotationMaxDegrees));

        HideAll();

        // 所有视觉状态由单一 TweenMethod 驱动。
        _tween = CreateTween();
        _tween.TweenMethod(Callable.From<float>(ApplyState), 0f, 1f, LifeTime);
        _tween.Finished += OnPlaybackFinished;
    }

    private void ApplyState(float progress)
    {
        var time = Mathf.Clamp(progress, 0f, 1f) * LifeTime;
        var frame = Mathf.Clamp((int)(time / FrameDuration), 0, SlashFrameCount - 1);

        ApplySlash(time, frame);
        ApplyGlow(time, frame);
        ApplyBigStar(time);
        ApplySmallStars(time);
    }

    private void ApplySlash(float time, int frame)
    {
        if (_frames.Length == SlashFrameCount)
            _slash.Texture = _frames[frame];

        var visible = time < SlashEnd;
        _slash.Visible = visible;
        if (!visible)
            return;

        var fade = time <= SlashFadeStart
            ? 1f
            : 1f - Mathf.Clamp((time - SlashFadeStart) / (SlashEnd - SlashFadeStart), 0f, 1f);
        _slash.Modulate = new Color(1f, 1f, 1f, fade);
    }

    private void ApplyGlow(float time, int frame)
    {
        var visible = time >= GlowStart;
        _glow.Visible = visible;
        if (!visible)
            return;

        // 辉光同步主斩帧序列，避免残留固定刀影。
        if (_frames.Length == SlashFrameCount)
            _glow.Texture = _frames[frame];

        var rise = Mathf.Clamp((time - GlowStart) / GlowRiseTime, 0f, 1f);
        var fade = time <= GlowFadeStart
            ? 1f
            : 1f - Mathf.Pow(Mathf.Clamp((time - GlowFadeStart) / (LifeTime - GlowFadeStart), 0f, 1f), 1.5f);
        var alpha = GlowPeakAlpha * rise * fade;

        _glow.Modulate = new Color(_glowBaseColor.R, _glowBaseColor.G, _glowBaseColor.B, alpha);
        var span = Mathf.Clamp((time - GlowStart) / (LifeTime - GlowStart), 0f, 1f);
        _glow.Scale = _glowBaseScale * Mathf.Lerp(1f, GlowScaleGrowth, EaseOutCubic(span));
    }

    private void ApplyBigStar(float time)
    {
        var visible = time >= BigStarStart && time < BigStarEnd;
        _bigStar.Visible = visible;
        if (!visible)
            return;

        var span = Mathf.Clamp((time - BigStarStart) / (BigStarEnd - BigStarStart), 0f, 1f);
        _bigStar.Scale = _bigStarBaseScale * Mathf.Lerp(BigStarPopScale, 1f, EaseOutCubic(span));

        float alpha;
        if (time < BigStarStart + BigStarRiseTime)
            alpha = Mathf.Clamp((time - BigStarStart) / BigStarRiseTime, 0f, 1f);
        else if (time <= BigStarHoldEnd)
            alpha = 1f;
        else
            alpha = 1f - Mathf.Clamp((time - BigStarHoldEnd) / (BigStarEnd - BigStarHoldEnd), 0f, 1f);

        _bigStar.Modulate = new Color(_bigStarBaseColor.R, _bigStarBaseColor.G, _bigStarBaseColor.B, alpha);
    }

    private void ApplySmallStars(float time)
    {
        var visible = time >= SmallStarStart;
        if (!visible)
            return;

        var span = Mathf.Clamp((time - SmallStarStart) / (LifeTime - SmallStarStart), 0f, 1f);
        var travel = EaseOutCubic(span);
        var alpha = time <= SmallStarFadeStart
            ? 1f
            : 1f - Mathf.Pow(
                Mathf.Clamp((time - SmallStarFadeStart) / (LifeTime - SmallStarFadeStart), 0f, 1f), 1.1f);

        for (var i = 0; i < _smallStars.Length && i < SmallStarConfigs.Length; i++)
        {
            var star = _smallStars[i];
            var config = SmallStarConfigs[i];
            var direction = Vector2.FromAngle(Mathf.DegToRad(config.AngleDegrees));

            star.Visible = true;
            star.Position = direction * config.Distance * travel;
            star.Scale = _smallStarBaseScales[i] * Mathf.Lerp(SmallStarPopScale, 1f, travel);
            // 星芒长轴沿局部 Y，径向对齐需旋转 φ - π/2。
            star.Rotation = Mathf.DegToRad(config.AngleDegrees) - Mathf.Pi * 0.5f;
            star.Modulate = new Color(
                _smallStarBaseColors[i].R, _smallStarBaseColors[i].G, _smallStarBaseColors[i].B, alpha);
        }
    }

    private void HideAll()
    {
        _glow.Visible = false;
        _slash.Visible = false;
        _bigStar.Visible = false;
        foreach (var star in _smallStars)
            star.Visible = false;
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

    private static float EaseOutCubic(float t)
    {
        var u = 1f - t;
        return 1f - u * u * u;
    }
}
