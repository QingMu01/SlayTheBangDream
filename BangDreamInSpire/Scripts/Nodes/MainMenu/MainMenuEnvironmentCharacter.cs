using BangDreamInSpire.Scripts.Utils;
using Godot;

namespace BangDreamInSpire.Scripts.Nodes.MainMenu;

public partial class MainMenuEnvironmentCharacter : Control
{
    private static string? _dayPath;
    private static string? _nightPath;

    /// <summary>注册环境角色日夜贴图并刷新现有实例。</summary>
    public static void ConfigureEnvironmentTextures(string? dayPath, string? nightPath)
    {
        _dayPath = dayPath;
        _nightPath = nightPath;
        _current?.ApplyCharacterByLocalTime();
    }

    /// <summary>点击弹性参数；缩放相对场景基准，底边固定。</summary>
    private sealed record ClickProfile(
        string Name,
        Vector2 PressScale,
        float PressDuration,
        (Vector2 Scale, float Duration)[] ReleaseSteps);

    /// <summary>进入主菜单时随机选择的参数组。</summary>
    private static readonly ClickProfile[] ClickProfiles =
    [
        new("轻点",
            new Vector2(1.06f, 0.93f), 0.06f,
            [
                (new Vector2(0.965f, 1.045f), 0.08f),
                (new Vector2(1.015f, 0.985f), 0.06f),
                (Vector2.One, 0.07f)
            ]),
        new("标准Q弹",
            new Vector2(1.12f, 0.86f), 0.07f,
            [
                (new Vector2(0.93f, 1.09f), 0.09f),
                (new Vector2(1.05f, 0.95f), 0.07f),
                (new Vector2(0.985f, 1.015f), 0.05f),
                (Vector2.One, 0.07f)
            ]),
        new("超级Q弹",
            new Vector2(1.20f, 0.78f), 0.09f,
            [
                (new Vector2(0.86f, 1.17f), 0.11f),
                (new Vector2(1.08f, 0.92f), 0.09f),
                (new Vector2(0.96f, 1.03f), 0.07f),
                (new Vector2(1.01f, 0.99f), 0.06f),
                (Vector2.One, 0.09f)
            ]),
        new("沉甸甸",
            new Vector2(1.14f, 0.84f), 0.11f,
            [
                (new Vector2(0.93f, 1.08f), 0.15f),
                (new Vector2(1.04f, 0.97f), 0.11f),
                (Vector2.One, 0.10f)
            ]),
        new("急弹",
            new Vector2(1.09f, 0.88f), 0.045f,
            [
                (new Vector2(0.94f, 1.07f), 0.06f),
                (new Vector2(1.03f, 0.97f), 0.05f),
                (Vector2.One, 0.05f)
            ])
    ];

    // 控制台指定的参数组，不写盘。
    private static int? _forcedProfileIndex;

    private static MainMenuEnvironmentCharacter? _current;

    /// <summary>控制台可用的参数组名。</summary>
    internal static string[] ClickProfileNames => [.. ClickProfiles.Select(profile => profile.Name)];

    /// <summary>控制台显示的参数摘要。</summary>
    internal static string[] ClickProfileDetails =>
    [
        .. ClickProfiles.Select(profile =>
            $"按下 ×{profile.PressScale.X:0.00}/×{profile.PressScale.Y:0.00} {profile.PressDuration:0.00}s" +
            $"，松开 {profile.ReleaseSteps.Sum(step => step.Duration):0.00}s")
    ];

    /// <summary>强制参数组序号，null 表示随机。</summary>
    internal static int? ForcedClickProfileIndex => _forcedProfileIndex;

    /// <summary>当前实例的参数组名。</summary>
    internal static string? CurrentClickProfileName => _current?._clickProfile?.Name;

    /// <summary>指定参数组并立即应用。</summary>
    internal static bool TryForceClickProfile(int index)
    {
        if (index < 0 || index >= ClickProfiles.Length)
            return false;

        _forcedProfileIndex = index;
        _current?.SetClickProfile(ClickProfiles[index]);
        return true;
    }

    /// <summary>恢复随机参数组。</summary>
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
            // 动画以场景静止缩放为基准。
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

        var path = IsDayTime(DateTime.Now) ? _dayPath : _nightPath;
        if (string.IsNullOrEmpty(path))
        {
            // 未注册贴图时保留场景占位。
            return;
        }

        if (!ResourceLoader.Exists(path))
        {
            BangDreamInSpireCore.Logger.Warn($"Main menu environment character texture not found: {path}");
            return;
        }

        _characterSprite.Texture = BangDreamPreloadManager.GetTexture2D(path);
        ApplyFeetPivot();
    }

    // 缩放原点固定在脚底。
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

    // 按下立即压扁。
    private void PlayPress()
    {
        if (_characterSprite == null || _clickProfile == null)
            return;

        _isPressed = true;
        _bounceTween?.Kill();
        _bounceTween = CreateTween();
        AddScaleStep(_bounceTween, _characterSprite, _clickProfile.PressScale, _clickProfile.PressDuration);
    }

    // 松开按阻尼回弹，连点时允许打断。
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

    // 关键帧使用相对倍率，从当前缩放连续过渡。
    private void AddScaleStep(Tween tween, Sprite2D sprite, Vector2 factor, float duration)
    {
        tween.TweenProperty(sprite, "scale", _restingScale * factor, duration)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
    }

    private static bool IsDayTime(DateTime time) => time.Hour is >= 6 and < 18;
}
