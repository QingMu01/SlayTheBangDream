using Godot;

namespace ItsCrychic.Scripts.Utils;

/// <summary>生成演奏区斜切平行四边形，统一处理右侧镜像、旋转与
/// ItemContainer 局部坐标；调用方持有缓冲以避免逐帧分配。</summary>
public static class PerformRowShape
{
    /// <summary>与 <c>parallelogram_clip.gdshader</c> 的 <c>slant_slope</c> 保持一致。</summary>
    public const float SlantSlope = 0.3f;

    public static float TopShift(Rect2 row) => SlantSlope * row.Size.Y;

    /// <summary>旋转支点为行的外上角，镜像列取右端。</summary>
    public static Vector2 HangPivot(Rect2 row, bool mirrored)
    {
        return new Vector2(mirrored ? row.End.X : row.Position.X, row.Position.Y);
    }

    /// <summary>将槽位本地 rotation 转为视觉倾角，镜像列取反。</summary>
    public static float VisualTilt(float localTilt, bool mirrored) => mirrored ? -localTilt : localTilt;

    /// <summary>顺时针写入四角；buffer 至少 4 项，tilt 为绕外上角的本地弧度。</summary>
    public static void GetCorners(Rect2 row, bool mirrored, float tilt, Vector2[] buffer)
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

        var angle = VisualTilt(tilt, mirrored);
        if (Mathf.Abs(angle) <= 0.0001f) return;

        var pivot = HangPivot(row, mirrored);
        for (var i = 0; i < 4; i++)
        {
            buffer[i] = pivot + (buffer[i] - pivot).Rotated(angle);
        }
    }

    /// <summary>返回斜切并倾斜后的顶边端点。</summary>
    public static void GetTopEdge(Rect2 row, bool mirrored, float tilt, out Vector2 from, out Vector2 to)
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

        var angle = VisualTilt(tilt, mirrored);
        if (Mathf.Abs(angle) <= 0.0001f) return;

        var pivot = HangPivot(row, mirrored);
        from = pivot + (from - pivot).Rotated(angle);
        to = pivot + (to - pivot).Rotated(angle);
    }

    /// <summary>填一块斜切四边形；<paramref name="buffer" /> 必须正好 4 项（<see cref="DrawColoredPolygon" /> 按整段数组画）。</summary>
    public static void Fill(CanvasItem target, Rect2 row, bool mirrored, float tilt, Color color,
        Vector2[] buffer)
    {
        if (color.A <= 0.001f) return;

        GetCorners(row, mirrored, tilt, buffer);
        target.DrawColoredPolygon(buffer, color);
    }

    /// <summary>描一圈斜切四边形；<paramref name="buffer" /> 必须正好 5 项（第 5 项为闭合点）。</summary>
    public static void Outline(CanvasItem target, Rect2 row, bool mirrored, float tilt, Color color,
        float width, Vector2[] buffer)
    {
        if (color.A <= 0.001f) return;

        GetCorners(row, mirrored, tilt, buffer);
        buffer[4] = buffer[0];
        target.DrawPolyline(buffer, color, width, true);
    }
}
