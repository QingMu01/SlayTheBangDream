using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace BangDreamLib.Scripts.Nodes.VFX;

/// <summary>
/// 祥子「打击」命中特效：原版 4 帧斩光重上色为白芯 + 蓝紫边，命中点爆出星辉
/// （大四芒星 + 6 颗小星外飞）。参考原版 <c>vfx_attack_slash</c> 的节奏与尺寸，
/// 星辉层借鉴摄政王 <c>vfx_starry_impact</c> 的「星核 + 星尘」。纯表现，基类 <see cref="Node2D" />。
/// 游戏内由 <c>WithHitVfxNode</c> 挂到目标生物身上，不参与任何结算。
/// </summary>
[Tool]
public partial class StrikeSakikoVfx : Node2D
{
    private const string ScenePath = "res://BangDreamLib/scenes/vfx/strike_sakiko_vfx.tscn";

    /// <summary>整段时长（含星辉淡出）。</summary>
    private const float LifeTime = 0.40f;

    /// <summary>与原版斩击一致的 15fps 帧间隔。</summary>
    private const float FrameDuration = 1f / 15f;

    private const int SlashFrameCount = 4;
    private const float SlashEnd = FrameDuration * SlashFrameCount;
    private const float SlashFadeStart = 0.233f;

    /// <summary>蓝紫辉光：与主刀光同帧出现，比斩光多留一段淡出。</summary>
    private const float GlowStart = 0.067f;
    private const float GlowRiseTime = 0.020f;
    private const float GlowPeakAlpha = 0.85f;
    private const float GlowFadeStart = 0.267f;
    private const float GlowScaleGrowth = 1.07f;

    /// <summary>星辉大四芒星：与主刀光同帧弹出，0.24s 前收掉。</summary>
    private const float BigStarStart = 0.067f;
    private const float BigStarRiseTime = 0.013f;
    private const float BigStarHoldEnd = 0.130f;
    private const float BigStarEnd = 0.240f;
    private const float BigStarPopScale = 0.58f;

    /// <summary>星尘：6 颗小星从命中点外飞，最后淡出。</summary>
    private const float SmallStarStart = 0.072f;
    private const float SmallStarFadeStart = 0.200f;
    private const float SmallStarPopScale = 0.6f;

    /// <summary>星尘的固定角度（度）与飞行距离（像素），顺序与场景节点 SmallStar0..5 对应。</summary>
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

    /// <summary>4 帧斩击贴图（本体 vfx_attack_slash_00..03），与场景里 Slash 节点的初始贴图一致。</summary>
    [Export] private Texture2D[] _frames = [];

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

    /// <summary>每次播放是否随机旋转整段特效（与原版打击的 0–25° 随机一致）。</summary>
    [Export] public bool RandomizeRotation { get; set; } = true;

    /// <summary>随机旋转上限（度）。预览中修改会立即重播。</summary>
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

    /// <summary>游戏内创建命中特效：定位与本体 <c>VfxCmd.PlayOnCreatureCenter</c> 相同。</summary>
    public static StrikeSakikoVfx? Create(Creature target)
    {
        if (TestMode.IsOn)
            return null;

        if (NCombatRoom.Instance?.GetCreatureNode(target) is not { } creatureNode)
            return null;

        var vfx = BangDreamPreloadManager.GetScene(ScenePath).Instantiate<StrikeSakikoVfx>();
        vfx.GlobalPosition = creatureNode.VfxSpawnPosition;
        return vfx;
    }

    public override void _Ready()
    {
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

        // 只读回基准值，不写任何视觉状态，编辑器里场景保持静止。
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

    /// <summary>重置并重新播放一次。</summary>
    public void Replay()
    {
        _tween?.Kill();

        // 预览时摆到视口中心，游戏内位置由调用方决定。
        VfxPreviewSupport.CenterForPreview(this);

        Rotation = _rootBaseRotation;
        if (RandomizeRotation)
            Rotation += Mathf.DegToRad((float)GD.RandRange(0.0, RandomRotationMaxDegrees));

        HideAll();

        // 铁律三：单一 TweenMethod 计算全部状态，不为同一属性建两个 tweener。
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

        // 辉光与主斩共用同一帧序列：若固定用第 1 帧，主斩消失后会留下一整块紫色刀影。
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
            // 星芒贴图长轴沿局部 Y 轴：对齐径向要旋转 φ - π/2，写成 φ 会横躺。
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
