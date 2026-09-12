using BangDreamLib.Scripts.Utils;
using Godot;

namespace BangDreamLib.Scripts.Nodes.MainMenu;

public partial class MainMenuEnvironmentCharacter : Control
{
    private static readonly StringName SampleUvA = "sample_uv_a";
    private static readonly StringName SampleUvB = "sample_uv_b";

    private const string DayPath = "res://BangDreamLib/images/sceneui/sakiko_day.png";
    private const string NightPath = "res://BangDreamLib/images/sceneui/sakiko_night.png";

    private const float BaseScale = 0.55f;

    // 点击Q弹节奏：竖向拉长 → 横向拉伸 → 缩小 → 放大 → 回正
    private static readonly (Vector2 Scale, float Duration)[] BounceSteps =
    [
        (new Vector2(0.55f, 0.72f), 0.10f),
        (new Vector2(0.68f, 0.66f), 0.10f),
        (new Vector2(0.50f, 0.50f), 0.09f),
        (new Vector2(0.58f, 0.58f), 0.08f),
        (new Vector2(0.55f, 0.55f), 0.08f)
    ];

    private Marker2D? _samplePointA;
    private Marker2D? _samplePointB;
    private Sprite2D? _characterSprite;
    private ShaderMaterial? _characterMaterial;
    private Control? _clickArea;
    private Tween? _bounceTween;
    private Vector2 _characterBasePosition;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _samplePointA = GetNodeOrNull<Marker2D>("%GradientSampleA");
        _samplePointB = GetNodeOrNull<Marker2D>("%GradientSampleB");
        _characterSprite = GetNodeOrNull<Sprite2D>("%Character");
        _characterMaterial = _characterSprite?.Material as ShaderMaterial;
        _clickArea = GetNodeOrNull<Control>("%ClickArea");

        if (_characterSprite != null)
            _characterBasePosition = _characterSprite.Position;

        ApplyCharacterByLocalTime();

        if (_clickArea != null)
            _clickArea.GuiInput += OnClickAreaGuiInput;

        if (_samplePointA == null || _samplePointB == null || _characterMaterial == null)
        {
            BangDreamLibCore.Logger.Warn(
                "Main menu environment character scene is missing a sample marker or ShaderMaterial.");
            SetProcess(false);
            return;
        }

        UpdateSampleUvs();
    }

    public override void _Process(double delta)
    {
        UpdateSampleUvs();
    }

    private void ApplyCharacterByLocalTime()
    {
        if (_characterSprite == null)
            return;

        var path = IsDayTime(DateTime.Now) ? DayPath : NightPath;
        if (!ResourceLoader.Exists(path))
        {
            BangDreamLibCore.Logger.Warn($"Main menu environment character texture not found: {path}");
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
            _characterBasePosition.Y + textureSize.Y * BaseScale / 2f);

        if (_clickArea == null)
            return;

        _clickArea.Position = new Vector2(
            _characterBasePosition.X - textureSize.X * BaseScale / 2f,
            _characterBasePosition.Y - textureSize.Y * BaseScale / 2f);
        _clickArea.Size = textureSize * BaseScale;
    }

    private void OnClickAreaGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
            PlayBounce();
    }

    private void PlayBounce()
    {
        if (_characterSprite == null)
            return;

        _bounceTween?.Kill();
        _bounceTween = CreateTween();
        foreach (var (scale, duration) in BounceSteps)
        {
            _bounceTween.TweenProperty(_characterSprite, "scale", scale, duration)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
        }
    }

    private static bool IsDayTime(DateTime time) => time.Hour is >= 6 and < 18;

    private void UpdateSampleUvs()
    {
        if (_samplePointA == null || _samplePointB == null || _characterMaterial == null)
            return;

        var visibleRect = GetViewport().GetVisibleRect();
        if (visibleRect.Size.X <= 0f || visibleRect.Size.Y <= 0f)
            return;

        _characterMaterial.SetShaderParameter(SampleUvA, ToScreenUv(_samplePointA, visibleRect));
        _characterMaterial.SetShaderParameter(SampleUvB, ToScreenUv(_samplePointB, visibleRect));
    }

    private static Vector2 ToScreenUv(CanvasItem samplePoint, Rect2 visibleRect)
    {
        var screenPosition = samplePoint.GetGlobalTransformWithCanvas().Origin - visibleRect.Position;
        return new Vector2(
            Mathf.Clamp(screenPosition.X / visibleRect.Size.X, 0f, 1f),
            Mathf.Clamp(screenPosition.Y / visibleRect.Size.Y, 0f, 1f));
    }
}
