using Godot;

namespace BangDreamInSpire.Scripts.Utils;

/// <summary>
/// 歌单冻结时「被提线吊住」的落位样式：每个槽位一组手调的倾角与位移，故意参差，
/// 让四角那几摞行看起来是被随手挂上去的，而不是一张对齐的网格。
/// <para>
/// 倾角是槽位节点的 <b>本地</b> rotation（弧度）：两列都取同一符号，右侧列因为水平镜像，
/// 视觉方向自然相反——两列都是「外端（绳结）不动、内端垂下去」。
/// </para>
/// <para>
/// 冻结层特效（提线、吊线、覆膜）与激活描边都读槽位实际的 rotation，所以这里的数值
/// 只由核心决定，内容模组的预览直接查同一张表即可，不需要各写一份。
/// </para>
/// </summary>
public static class PerformHangStyle
{
    /// <summary>
    /// 每个槽位的手工偏差：<c>TiltDeg</c> 倾角（度）、<c>Outward</c> 向列外端位移、<c>Drop</c> 下沉量（像素）。
    /// 槽位号按 1 起算、超出表长时循环取用。
    /// </summary>
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

    /// <summary>该槽位冻结时的倾角（弧度，槽位节点本地 rotation）。</summary>
    public static float TiltRadians(int slotIndex)
    {
        return Mathf.DegToRad(VariantOf(slotIndex).TiltDeg);
    }

    /// <summary>该槽位冻结时相对布局位置的位移（已按列镜像取向外端）。</summary>
    public static Vector2 Offset(int slotIndex, bool mirroredCorner)
    {
        var variant = VariantOf(slotIndex);
        var outward = mirroredCorner ? variant.Outward : -variant.Outward;
        return new Vector2(outward, variant.Drop);
    }
}
