using Godot;

namespace BangDreamInSpire.Scripts.Utils;

/// <summary>冻结槽位的本地倾角与位移表，预览与游戏共用；镜像列视觉方向相反。</summary>
public static class PerformHangStyle
{
    /// <summary>槽位偏差，超出表长循环取用。</summary>
    private static readonly (float TiltDeg, float Outward, float Drop)[] Variants =
    [
        (2.4f, 3.4f, 2.7f),
        (0.9f, 1.3f, 0.7f),
        (1.8f, 4.1f, 3.4f),
        (2.1f, 2.1f, 1.1f),
        (3.0f, 3.7f, 2.9f),
        (1.1f, 1.6f, 0.5f),
        (2.6f, 2.8f, 3.8f),
        (1.4f, 1.1f, 1.7f),
        (2.2f, 3.6f, 0.9f),
        (0.8f, 2.0f, 2.3f),
        (2.8f, 3.0f, 1.0f),
        (1.9f, 2.6f, 3.2f),
        (2.3f, 1.9f, 2.0f),
        (1.2f, 3.3f, 1.4f),
        (2.7f, 2.4f, 3.6f),
        (1.6f, 4.0f, 0.8f)
    ];

    private static (float TiltDeg, float Outward, float Drop) VariantOf(int slotIndex)
    {
        var length = Variants.Length;
        var index = (slotIndex - 1) % length;
        if (index < 0) index += length;
        return Variants[index];
    }

    /// <summary>返回槽位本地倾角。</summary>
    public static float TiltRadians(int slotIndex)
    {
        return Mathf.DegToRad(VariantOf(slotIndex).TiltDeg);
    }

    /// <summary>返回按镜像方向调整的位移。</summary>
    public static Vector2 Offset(int slotIndex, bool mirroredCorner)
    {
        var variant = VariantOf(slotIndex);
        var outward = mirroredCorner ? variant.Outward : -variant.Outward;
        return new Vector2(outward, variant.Drop);
    }
}
