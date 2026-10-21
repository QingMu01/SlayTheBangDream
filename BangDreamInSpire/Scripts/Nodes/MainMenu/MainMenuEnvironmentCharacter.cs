using BangDreamInSpire.Scripts.Utils;
using Godot;

namespace BangDreamInSpire.Scripts.Nodes.MainMenu;

public partial class MainMenuEnvironmentCharacter : Control
{
    private const string DayPath = "res://BangDreamInSpire/images/sceneui/sakiko_day.png";
    private const string NightPath = "res://BangDreamInSpire/images/sceneui/sakiko_night.png";

    /// <summary>
    /// 一次点击的弹性参数。缩放字段是相对场景静止缩放（<see cref="_restingScale"/>）的倍率，
    /// 底边固定在脚底。
    /// 手感参考「蓝色大肥鱼 Q 弹」素材的挤压 / 回弹节奏——该素材是旋转中不断被压扁、拉长，
    /// 2D 角色不取旋转，只复刻这组挤压与阻尼收敛。
    /// </summary>
    private sealed record ClickProfile(
        string Name,
        Vector2 PressScale,
        float PressDuration,
        (Vector2 Scale, float Duration)[] ReleaseSteps);

    /// <summary>候选手感；每次进入主菜单随机挑一组，避免每次点击都是同一种节奏。</summary>
    private static readonly ClickProfile[] ClickProfiles =
    [
        // 轻点：幅度最小，最不打扰
        new("轻点",
            new Vector2(1.06f, 0.93f), 0.06f,
            [
                (new Vector2(0.965f, 1.045f), 0.08f),
                (new Vector2(1.015f, 0.985f), 0.06f),
                (Vector2.One, 0.07f)
            ]),
        // 标准Q弹：一次明显过冲 + 一次小幅回摆
        new("标准Q弹",
            new Vector2(1.12f, 0.86f), 0.07f,
            [
                (new Vector2(0.93f, 1.09f), 0.09f),
                (new Vector2(1.05f, 0.95f), 0.07f),
                (new Vector2(0.985f, 1.015f), 0.05f),
                (Vector2.One, 0.07f)
            ]),
        // 超级Q弹：压得最扁、弹得最开，最接近素材那种胖乎乎的挤压感
        new("超级Q弹",
            new Vector2(1.20f, 0.78f), 0.09f,
            [
                (new Vector2(0.86f, 1.17f), 0.11f),
                (new Vector2(1.08f, 0.92f), 0.09f),
                (new Vector2(0.96f, 1.03f), 0.07f),
                (new Vector2(1.01f, 0.99f), 0.06f),
                (Vector2.One, 0.09f)
            ]),
        // 沉甸甸：压得慢、回弹也慢，适合表现体重
        new("沉甸甸",
            new Vector2(1.14f, 0.84f), 0.11f,
            [
                (new Vector2(0.93f, 1.08f), 0.15f),
                (new Vector2(1.04f, 0.97f), 0.11f),
                (Vector2.One, 0.10f)
            ]),
        // 急弹：总时长最短，快速连点时最跟手
        new("急弹",
            new Vector2(1.09f, 0.88f), 0.045f,
            [
                (new Vector2(0.94f, 1.07f), 0.06f),
                (new Vector2(1.03f, 0.97f), 0.05f),
                (Vector2.One, 0.05f)
            ])
    ];

    // 控制台调试用：强制指定一组手感（null = 每次 Ready 随机）。只存在内存里，不写盘。
    private static int? _forcedProfileIndex;

    // 当前主菜单里的角色实例，控制台指令切换手感时要立刻作用到它身上
    private static MainMenuEnvironmentCharacter? _current;

    /// <summary>全部参数组名，供控制台指令列出 / 按名字匹配。</summary>
    internal static string[] ClickProfileNames => [.. ClickProfiles.Select(profile => profile.Name)];

    /// <summary>逐组的一行数值摘要（按下倍率/时长、松开总时长），供控制台指令列出。</summary>
    internal static string[] ClickProfileDetails =>
    [
        .. ClickProfiles.Select(profile =>
            $"按下 ×{profile.PressScale.X:0.00}/×{profile.PressScale.Y:0.00} {profile.PressDuration:0.00}s" +
            $"，松开 {profile.ReleaseSteps.Sum(step => step.Duration):0.00}s")
    ];

    /// <summary>当前强制指定的参数组序号；null 表示随机。</summary>
    internal static int? ForcedClickProfileIndex => _forcedProfileIndex;

    /// <summary>当前实例正在使用的参数组名；主菜单里没有实例时为 null。</summary>
    internal static string? CurrentClickProfileName => _current?._clickProfile?.Name;

    /// <summary>强制指定一组手感并立刻作用到当前实例；序号越界返回 false。</summary>
    internal static bool TryForceClickProfile(int index)
    {
        if (index < 0 || index >= ClickProfiles.Length)
            return false;

        _forcedProfileIndex = index;
        _current?.SetClickProfile(ClickProfiles[index]);
        return true;
    }

    /// <summary>取消强制指定：下次进入主菜单重新随机，当前实例也立刻换一组随机手感。</summary>
    internal static void ClearForcedClickProfile()
    {
        _forcedProfileIndex = null;
        _current?.SetClickProfile(ClickProfiles[GD.RandRange(0, ClickProfiles.Length - 1)]);
    }

    private Sprite2D? _characterSprite;
    private Control? _clickArea;
    private Tween? _bounceTween;
    private ClickProfile? _clickProfile;
    private bool _isPressed;
    private Vector2 _restingScale = Vector2.One;
    private Vector2 _characterBasePosition;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _characterSprite = GetNodeOrNull<Sprite2D>("%Character");
        _clickArea = GetNodeOrNull<Control>("%ClickArea");

        if (_characterSprite != null)
        {
            // 动画与布局都从场景读回的静止缩放出发，不硬编码美术基准值
            _restingScale = _characterSprite.Scale;
            _characterBasePosition = _characterSprite.Position;
        }

        _current = this;
        SetClickProfile(_forcedProfileIndex is { } forced
            ? ClickProfiles[forced]
            : ClickProfiles[GD.RandRange(0, ClickProfiles.Length - 1)]);

        ApplyCharacterByLocalTime();

        if (_clickArea != null)
            _clickArea.GuiInput += OnClickAreaGuiInput;
    }

    public override void _ExitTree()
    {
        if (_current == this)
            _current = null;
    }

    private void SetClickProfile(ClickProfile profile)
    {
        _clickProfile = profile;
        BangDreamInSpireCore.Logger.Info($"Main menu environment character click profile: {profile.Name}");
    }

    private void ApplyCharacterByLocalTime()
    {
        if (_characterSprite == null)
            return;

        var path = IsDayTime(DateTime.Now) ? DayPath : NightPath;
        if (!ResourceLoader.Exists(path))
        {
            BangDreamInSpireCore.Logger.Warn($"Main menu environment character texture not found: {path}");
            return;
        }

        _characterSprite.Texture = BangDreamPreloadManager.GetTexture2D(path);
        ApplyFeetPivot();
    }

    // 缩放原点移到脚底：底边落在节点原点，缩放变化时脚底不动
    private void ApplyFeetPivot()
    {
        if (_characterSprite?.Texture == null)
            return;

        var textureSize = _characterSprite.Texture.GetSize();
        _characterSprite.Offset = new Vector2(0f, -textureSize.Y / 2f);
        _characterSprite.Position = new Vector2(
            _characterBasePosition.X,
            _characterBasePosition.Y + textureSize.Y * _restingScale.Y / 2f);

        if (_clickArea == null)
            return;

        _clickArea.Position = new Vector2(
            _characterBasePosition.X - textureSize.X * _restingScale.X / 2f,
            _characterBasePosition.Y - textureSize.Y * _restingScale.Y / 2f);
        _clickArea.Size = textureSize * _restingScale;
    }

    private void OnClickAreaGuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouseButton)
            return;

        if (mouseButton.Pressed)
            PlayPress();
        else if (_isPressed)
            PlayRelease();
    }

    // 按下：立刻压扁，像被按进地面
    private void PlayPress()
    {
        if (_characterSprite == null || _clickProfile == null)
            return;

        _isPressed = true;
        _bounceTween?.Kill();
        _bounceTween = CreateTween();
        AddScaleStep(_bounceTween, _characterSprite, _clickProfile.PressScale, _clickProfile.PressDuration);
    }

    // 松开：过冲 → 反向挤压 → 阻尼收敛；快速连点时下一次按下会直接打断这串关键帧
    private void PlayRelease()
    {
        if (_characterSprite == null || _clickProfile == null)
            return;

        _isPressed = false;
        _bounceTween?.Kill();
        _bounceTween = CreateTween();

        foreach (var (scale, duration) in _clickProfile.ReleaseSteps)
            AddScaleStep(_bounceTween, _characterSprite, scale, duration);
    }

    // 关键帧都是"倍率"，统一乘回静止缩放；从当前 scale 起步，打断时不会跳变
    private void AddScaleStep(Tween tween, Sprite2D sprite, Vector2 factor, float duration)
    {
        tween.TweenProperty(sprite, "scale", _restingScale * factor, duration)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
    }

    private static bool IsDayTime(DateTime time) => time.Hour is >= 6 and < 18;
}
