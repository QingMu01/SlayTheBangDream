using Godot;

namespace BangDreamLib.Scripts.Utils;

public class CardMaterialHelper
{
    private static Shader? _shader;

    private static Shader ColorOverlayShader =>
        _shader ??= ResourceLoader.Load<Shader>("res://BangDreamLib/shaders/color_overlay.gdshader");

    public static Material CreateColorOverlayMaterial(float r, float g, float b,
        float darkThreshold = 0.2f,
        float darkFalloff = 0.2f,
        float darkIntensity = 0.0f
    )
    {
        var shaderMaterial = new ShaderMaterial();
        shaderMaterial.Shader = ColorOverlayShader;
        shaderMaterial.SetShaderParameter("overlay_color", new Color(r, g, b));
        shaderMaterial.SetShaderParameter("dark_threshold", darkThreshold);
        shaderMaterial.SetShaderParameter("dark_falloff", darkFalloff);
        shaderMaterial.SetShaderParameter("dark_intensity", darkIntensity);
        return shaderMaterial;
    }
}