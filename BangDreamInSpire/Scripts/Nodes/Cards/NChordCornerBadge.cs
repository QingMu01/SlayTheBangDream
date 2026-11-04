using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Mechanics.Perform;
using BangDreamInSpire.Scripts.Mechanics.Perform.Chord;
using Godot;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Models.Capabilities;

namespace BangDreamInSpire.Scripts.Nodes.Cards;

/// <summary>
/// 卡牌右上角的和弦包角带。
/// <para>
/// 把卡牌右上角建模为一条沿「上边 + 右边」的**等臂** L 形包角带（两臂都从拐角量 120px）。
/// 多个和弦时用互相平行的 **45° 斜切线**（<c>x + y = c</c>，与拐角斜接缝同族）把带子**按面积均分**：
/// n 个和弦得到 n 段，每段面积严格相等，切缝一律是斜的，视觉上是一组斜切的色带。
/// <list type="bullet">
/// <item>1 个和弦：整条 L 形带；</item>
/// <item>2 个和弦：一条斜接缝正好落在拐角（外角到内角的连线），上臂 / 右臂各一段；</item>
/// <item>3 个和弦：上臂一段 + 绕拐角的中间段 + 右臂一段，三条缝等距；</item>
/// <item>4 个和弦：上臂两段 + 右臂两段，拐角处仍是斜接缝。</item>
/// </list>
/// </para>
/// <para>
/// 渲染：首版用单张白色包角带贴图，把每段的多边形作为 <see cref="Polygon2D" /> 的裁剪形状，
/// 顶点 UV 直接映射到贴图，再用 <see cref="Polygon2D.Color" /> 染成和弦色；贴图未就位时
/// 同一个多边形退化为纯色填充，形状与成品完全一致，可直接用来验收切分。
/// </para>
/// </summary>
public partial class NChordCornerBadge : Control
{
    /// <summary>
    /// 白色包角带贴图路径。美术契约：画布是 <see cref="BadgeBoundsInCard" /> 的正方形（120×120 at 1×，
    /// 按 <see cref="TextureScale" /> 倍导出）；带子沿画布上边（高 = 带宽）与右边（宽 = 带宽）贴合，
    /// 外角不倒角、内侧可圆角，带外区域全透明。贴图缺失时本项目退化为纯色多边形。
    /// </summary>
    public const string TexturePath = "res://BangDreamInSpire/images/sceneui/chord_corner.png";

    /// <summary>卡牌角标的节点名，便于调试与查找。</summary>
    public const string NodeName = "ChordCornerBadge";

    /// <summary>包角带贴图相对卡面像素的倍率（美术按 2 倍导出）。</summary>
    private const float TextureScale = 2f;

    /// <summary>包角带的宽度（臂厚）。</summary>
    private const float BandThickness = 26f;

    /// <summary>两臂等长：都从拐角量 120px。</summary>
    private const float ArmLength = 120f;

    /// <summary>卡框右边缘（<c>CardContainer</c> 坐标系，原点 = 卡牌中心）。</summary>
    private const float CardRight = 150f;

    /// <summary>卡框上边缘。</summary>
    private const float CardTop = -211f;

    /// <summary>包角带左端（上臂起点）。</summary>
    private const float BandLeft = CardRight - ArmLength;

    /// <summary>包角带下端（右臂终点）。</summary>
    private const float BandBottom = CardTop + ArmLength;

    /// <summary>斜切线族 <c>x + y = c</c> 在带子上取到的最小 / 最大值。</summary>
    private const float MinDiagonal = BandLeft + CardTop;
    private const float MaxDiagonal = CardRight + BandBottom;

    /// <summary>带子总面积（两臂之和减去重叠的拐角方块）。</summary>
    private const float BandArea = 2f * ArmLength * BandThickness - BandThickness * BandThickness;

    /// <summary>角标在卡面坐标系中的包围盒：贴图应恰好铺满这个正方形。</summary>
    public static readonly Rect2 BadgeBoundsInCard = new(BandLeft, CardTop, ArmLength, ArmLength);

    /// <summary>整条等臂 L 形包角带（6 个顶点，凹多边形，顺时针）。切缝直接用它裁剪，避免拐角处重复叠画。</summary>
    private static readonly Vector2[] BandPolygon =
    [
        new(BandLeft, CardTop),
        new(CardRight, CardTop),
        new(CardRight, BandBottom),
        new(CardRight - BandThickness, BandBottom),
        new(CardRight - BandThickness, CardTop + BandThickness),
        new(BandLeft, CardTop + BandThickness)
    ];

    private Texture2D? _bandTexture;
    private bool _bandTextureLoaded;

    /// <summary>
    /// 为指定卡牌创建角标节点；无和弦时返回仍不可见的空节点（由 <see cref="Refresh" /> 决定是否显示）。
    /// </summary>
    public static NChordCornerBadge Create(CardModel card)
    {
        var badge = new NChordCornerBadge
        {
            Name = NodeName,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 1 // 抬到标题横幅之上：能力覆盖层挂在 OverlayContainer 下，层级比 TitleBanner 低
        };
        badge.Refresh(card);
        return badge;
    }

    /// <summary>
    /// 按和弦数量返回每个和弦占用的多边形（卡面坐标系）。
    /// 所有切缝都平行于斜接线 <c>x + y = c</c>，切点由「按面积均分」反解得到，因此各段面积严格相等。
    /// </summary>
    public static List<Vector2[]>[] BuildSlots(int chordCount)
    {
        if (chordCount <= 0)
        {
            return [];
        }

        var slots = new List<Vector2[]>[chordCount];
        for (var index = 0; index < chordCount; index++)
        {
            // 第 index 段的斜切区间（首段无下界，末段无上界）
            var lower = index == 0 ? float.NegativeInfinity : SolveCut(BandArea * index / chordCount);
            var upper = index == chordCount - 1 ? float.PositiveInfinity : SolveCut(BandArea * (index + 1) / chordCount);

            var polygon = BandPolygon.ToList();
            if (!float.IsNegativeInfinity(lower))
            {
                polygon = ClipByDiagonal(polygon, lower, keepAbove: true);
            }

            if (!float.IsPositiveInfinity(upper))
            {
                polygon = ClipByDiagonal(polygon, upper, keepAbove: false);
            }

            slots[index] = polygon.Count >= 3 ? [polygon.ToArray()] : [];
        }

        return slots;
    }

    /// <summary>按卡牌当前的和弦重建角标；无和弦时隐藏。</summary>
    public void Refresh(CardModel? card)
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        var chord = card != null && card.TryGetCapability<ChordCapability>(out var capability)
            ? capability.Chord
            : PerformChord.None;
        var groups = chord.EnumerateGroups().ToArray();
        if (groups.Length == 0)
        {
            Visible = false;
            return;
        }

        Visible = true;
        var slots = BuildSlots(groups.Length);
        for (var index = 0; index < groups.Length && index < slots.Length; index++)
        {
            var color = PerformChordColors.ColorOf(groups[index]);
            foreach (var piece in slots[index])
            {
                AddChild(CreateBandPiece(piece, color));
            }
        }
    }

    /// <summary>Sutherland–Hodgman：用斜线 <c>x + y = c</c> 裁凸多边形，保留指定一侧。</summary>
    private static List<Vector2> ClipByDiagonal(List<Vector2> polygon, float diagonal, bool keepAbove)
    {
        var result = new List<Vector2>(polygon.Count + 1);
        for (var i = 0; i < polygon.Count; i++)
        {
            var current = polygon[i];
            var next = polygon[(i + 1) % polygon.Count];
            var distanceCurrent = current.X + current.Y - diagonal;
            var distanceNext = next.X + next.Y - diagonal;
            var currentInside = keepAbove ? distanceCurrent >= 0f : distanceCurrent <= 0f;
            var nextInside = keepAbove ? distanceNext >= 0f : distanceNext <= 0f;

            if (currentInside)
            {
                result.Add(current);
            }

            if (currentInside != nextInside)
            {
                result.Add(current.Lerp(next, distanceCurrent / (distanceCurrent - distanceNext)));
            }
        }

        return result;
    }

    /// <summary>二分反解：求斜切常量 c，使 <c>x + y ≤ c</c> 的部分面积为 <paramref name="targetArea" />。</summary>
    private static float SolveCut(float targetArea)
    {
        var low = MinDiagonal - 1f;
        var high = MaxDiagonal + 1f;
        for (var iteration = 0; iteration < 48; iteration++)
        {
            var middle = (low + high) * 0.5f;
            if (AreaBelow(middle) < targetArea)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return (low + high) * 0.5f;
    }

    /// <summary>带子在斜线 <c>x + y ≤ c</c> 下方的面积（两臂相加减去重叠方块）。</summary>
    private static float AreaBelow(float diagonal) =>
        RectAreaBelow(CardRight - BandThickness, CardRight, CardTop, BandBottom, diagonal)
        + RectAreaBelow(BandLeft, CardRight, CardTop, CardTop + BandThickness, diagonal)
        - RectAreaBelow(CardRight - BandThickness, CardRight, CardTop, CardTop + BandThickness, diagonal);

    /// <summary>矩形内满足 <c>x + y ≤ diagonal</c> 的面积（对每列取 clamp 的高度积分，闭式）。</summary>
    private static float RectAreaBelow(float x0, float x1, float y0, float y1, float diagonal)
    {
        var height = y1 - y0;
        var shift = diagonal - y0; // 每列高度 = clamp(shift - x, 0, height)

        double Primitive(double x)
        {
            if (x <= shift - height)
            {
                return height * x;
            }

            var full = height * (shift - height);
            if (x >= shift)
            {
                return full + height * height / 2d;
            }

            // shift-height < x < shift：∫(shift - u)du
            return full + (shift * x - x * x / 2d) - (shift * (shift - height) - (shift - height) * (shift - height) / 2d);
        }

        return (float)(Primitive(x1) - Primitive(x0));
    }

    /// <summary>
    /// 创建包角带的一段：有贴图时按多边形裁切并染色，没有贴图时退回纯色多边形。
    /// 凹多边形（3 个和弦的中间段）先拆成凸块再交给 <see cref="Polygon2D" />，避免三角化差异。
    /// </summary>
    private CanvasItem CreateBandPiece(Vector2[] vertices, Color color)
    {
        var texture = GetBandTexture();
        var container = new Node2D();
        foreach (var part in Decompose(vertices))
        {
            var polygon = new Polygon2D
            {
                Polygon = part,
                Color = color,
                Texture = texture
            };

            if (texture != null)
            {
                // 顶点是卡面坐标，先平移成「相对角标画布」的坐标，再乘贴图倍率
                var uvs = new Vector2[part.Length];
                for (var i = 0; i < part.Length; i++)
                {
                    uvs[i] = (part[i] - BadgeBoundsInCard.Position) * TextureScale;
                }

                polygon.UV = uvs;
            }

            container.AddChild(polygon);
        }

        return container;
    }

    /// <summary>把槽位多边形拆成凸块；已经凸的会原样返回。</summary>
    private static Vector2[][] Decompose(Vector2[] vertices)
    {
        var convexParts = Geometry2D.DecomposePolygonInConvex(vertices);
        return convexParts.Count > 0 ? convexParts.ToArray() : [vertices];
    }

    /// <summary>懒加载包角带贴图；文件缺失时返回 <see langword="null" /> 让调用方走占位分支。</summary>
    private Texture2D? GetBandTexture()
    {
        if (_bandTextureLoaded)
        {
            return _bandTexture;
        }

        _bandTextureLoaded = true;
        if (ResourceLoader.Exists(TexturePath))
        {
            _bandTexture = GD.Load<Texture2D>(TexturePath);
        }

        return _bandTexture;
    }
}
