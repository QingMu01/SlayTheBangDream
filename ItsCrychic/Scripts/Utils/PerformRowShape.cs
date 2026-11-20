using Godot;

namespace ItsCrychic.Scripts.Utils;

/// <summary>
/// 演奏区「行」的视觉形状：行不是矩形，而是 <c>parallelogram_clip.gdshader</c> 裁出来的斜切平行四边形。
/// <para>
/// 着色器的 <c>slant_slope = 0.3</c> 让顶边相对底边水平错开 <c>slant × 行高</c>（35px 高时约 10.5px）；
/// 右侧列整体水平镜像，斜切方向随之翻转。凡是要画在行上的东西（冻结覆膜、激活描边、落位高亮）
/// 都要走这里，按矩形画会让四角戳在行外面。
/// </para>
/// <para>
/// 坐标与 <c>NPerformItem.Position</c> 同一空间（演奏区 <c>ItemContainer</c> 局部坐标）。
/// 缓冲由调用方持有，避免逐帧分配。
/// </para>
/// </summary>
public static class PerformRowShape
{
    /// <summary>与 <c>parallelogram_clip.gdshader</c> 的 <c>slant_slope</c> 保持一致。</summary>
    public const float SlantSlope = 0.3f;

    /// <summary>顶边相对底边的水平错开量。</summary>
    public static float TopShift(Rect2 row) => SlantSlope * row.Size.Y;

    /// <summary>写入行的四个角（左上、右上、右下、左下，顺时针）；<paramref name="buffer" /> 至少 4 项。</summary>
    public static void GetCorners(Rect2 row, bool mirrored, Vector2[] buffer)
    {
        var shift = TopShift(row);
        var left = row.Position.X;
        var right = row.End.X;
        var top = row.Position.Y;
        var bottom = row.End.Y;

        if (mirrored)
        {
            buffer[0] = new Vector2(left, top);
            buffer[1] = new Vector2(right - shift, top);
            buffer[2] = new Vector2(right, bottom);
            buffer[3] = new Vector2(left + shift, bottom);
        }
        else
        {
            buffer[0] = new Vector2(left + shift, top);
            buffer[1] = new Vector2(right, top);
            buffer[2] = new Vector2(right - shift, bottom);
            buffer[3] = new Vector2(left, bottom);
        }
    }

    /// <summary>顶边（被提起来的那条边）的两个端点，按斜切内缩。</summary>
    public static void GetTopEdge(Rect2 row, bool mirrored, out Vector2 from, out Vector2 to)
    {
        var shift = TopShift(row);
        var top = row.Position.Y;
        if (mirrored)
        {
            from = new Vector2(row.Position.X, top);
            to = new Vector2(row.End.X - shift, top);
        }
        else
        {
            from = new Vector2(row.Position.X + shift, top);
            to = new Vector2(row.End.X, top);
        }
    }

    /// <summary>填一块斜切四边形；<paramref name="buffer" /> 必须正好 4 项（<see cref="DrawColoredPolygon" /> 按整段数组画）。</summary>
    public static void Fill(CanvasItem target, Rect2 row, bool mirrored, Color color, Vector2[] buffer)
    {
        if (color.A <= 0.001f) return;

        GetCorners(row, mirrored, buffer);
        target.DrawColoredPolygon(buffer, color);
    }

    /// <summary>描一圈斜切四边形；<paramref name="buffer" /> 必须正好 5 项（第 5 项为闭合点）。</summary>
    public static void Outline(CanvasItem target, Rect2 row, bool mirrored, Color color, float width,
        Vector2[] buffer)
    {
        if (color.A <= 0.001f) return;

        GetCorners(row, mirrored, buffer);
        buffer[4] = buffer[0];
        target.DrawPolyline(buffer, color, width, true);
    }
}
