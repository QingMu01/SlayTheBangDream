using BangDreamLib.Scripts.Utils;
using Godot;

namespace BangDreamLib.Scripts.Nodes.MainMenu;

public partial class MainMenuEnvironmentCharacter : Control
{
    private static readonly StringName SampleUvA = "sample_uv_a";
    private static readonly StringName SampleUvB = "sample_uv_b";

    private const string DayPath = "res://BangDreamLib/images/sceneui/sakiko_day.png";
    private const string NightPath = "res://BangDreamLib/images/sceneui/sakiko_night.png";

    private Marker2D? _samplePointA;
    private Marker2D? _samplePointB;
    private Sprite2D? _characterSprite;
    private ShaderMaterial? _characterMaterial;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _samplePointA = GetNodeOrNull<Marker2D>("%GradientSampleA");
        _samplePointB = GetNodeOrNull<Marker2D>("%GradientSampleB");
        _characterSprite = GetNodeOrNull<Sprite2D>("%Character");
        _characterMaterial = _characterSprite?.Material as ShaderMaterial;

        ApplyCharacterByLocalTime();

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