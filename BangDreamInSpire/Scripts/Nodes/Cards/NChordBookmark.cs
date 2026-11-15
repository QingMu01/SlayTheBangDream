using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Mechanics.Perform;
using BangDreamInSpire.Scripts.Utils.Infos;
using BangDreamInSpire.Scripts.Mechanics.Perform.Chord;
using Godot;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Models.Capabilities;

namespace BangDreamInSpire.Scripts.Nodes.Cards;

/// <summary>
/// 卡牌右边缘的箭头型和弦书签（ribbon bookmark），垂直排列。
/// <para>
/// 一枚书签 = 贴在卡框<b>右边缘</b>的一条竖直色带 + 右端向右凸出的 <c>&gt;</c> 形箭头尖端；
/// 左端压入卡框内侧 <see cref="BookmarkInset" /> 像素，让色带看起来是「从卡面里伸出来的一截书签」，
/// 而不是整枚漂在卡外的贴纸。多个和弦（掩码）时按 <c>C→D→F→G</c> 的顺序从上到下竖直堆叠，
/// 枚与枚之间留 <see cref="BookmarkGap" /> 像素缝，整组在卡框右边缘<b>垂直居中</b>。
/// </para>
/// <para>
/// 几何契约（<c>CardContainer</c> 坐标系，原点 = 卡牌中心，<b>y 向下</b>，卡框 300×422）：
/// <list type="bullet">
/// <item>卡框右边缘 <c>x = <see cref="CardRight" /></c>（=150）、上 / 下边缘 <c>y = ∓211</c>；</item>
/// <item>单枚书签矩形段：<c>x ∈ [146, 156]</c>（左端压入卡框 4px、右端伸出卡框 6px），高度 20px；</item>
/// <item>箭头尖端：在矩形段高度中点再向右凸出到 <c>x = 166</c>（相对卡框右边缘外凸 16px），
/// 两条斜边从矩形段右上 / 右下角收束到尖端，形成 <c>&gt;</c> 形；</item>
/// <item>n 枚时整组高度 = <c>n × 20 + (n − 1) × 4</c>，组顶 <c>y = −组高 / 2</c>（1 枚 −10、4 枚 −46），
/// 因此 1~4 枚都不会碰到标题横幅（横幅占 <c>y ∈ [−207, −124]</c>，卡面标题区在 y&lt;−124）；
/// 另外组顶还有 <see cref="MinGroupTopY" /> = −160 的硬下限，任何情况下都不许把书签推到横幅高度上去。</item>
/// </list>
/// 同一套几何由 <see cref="BuildBookmarkRects" /> / <see cref="BuildBookmarkShape" /> 对外暴露，
/// <c>.scratch/chord-bookmark/preview.png</c>（2× 静态排版预览）按这两个方法的同一组常量绘制，便于不进游戏核对排版。
/// </para>
/// <para>
/// 渲染：首版<b>纯程序化多边形</b>（<see cref="Polygon2D" />），颜色唯一来源是
/// <see cref="PerformChordColors.ColorOf" />（与演奏区槽位同源），外圈再加一条同色系加深的
/// <see cref="Line2D" /> 描边，保证在深色 / 浅色卡面上都能读出边界。贴图通道见 <see cref="TexturePath" />：
/// 贴图缺位时同一多边形退化为纯色填充，形状与成品完全一致，可直接用来验收排版。
/// </para>
/// <para>
/// 层级：书签挂在卡面的 <c>%OverlayContainer</c> 下，该节点在 <c>card.tscn</c> 里排在
/// <c>TitleBanner</c> / <c>TitleLabel</c> <b>之前</b>——也就是书签在图层上位于卡面标题之下，
/// 这是<b>设计预期</b>：书签是「贴在卡框侧边的书签」，不抢卡面前景。不要用
/// <see cref="CanvasItem.ZIndex" /> 去抬高它（Godot 的 z_index 在同一 canvas 内全局排序，
/// 会把书签抬到面板 / HUD 之上），也不要重排 <c>%OverlayContainer</c> 的画布顺序。
/// </para>
/// </summary>
public partial class NChordBookmark : Control
{
    /// <summary>
    /// 书签贴图路径（可选通道）。美术契约：画布是<b>单枚书签</b>的包围盒
    /// （<see cref="BookmarkCanvasInCard" />，20×20 at 1×），按 <see cref="TextureScale" /> 倍导出；
    /// 画布内左侧 4px 是压进卡框的部分、右侧 6px 矩形段 + 10px 箭头尖端，画布外区域全透明。
    /// 文件缺失时本项目退化为纯色多边形，与本类的程序化形状完全一致。
    /// </summary>
    public const string TexturePath = "res://BangDreamInSpire/images/sceneui/chord_bookmark.png";

    /// <summary>书签节点的节点名，便于调试与查找。</summary>
    public const string NodeName = "ChordBookmark";

    /// <summary>书签贴图相对卡面像素的倍率（美术按 2 倍导出）。</summary>
    private const float TextureScale = 2f;

    /// <summary>卡框右边缘（<c>CardContainer</c> 坐标系，原点 = 卡牌中心）。</summary>
    private const float CardRight = 150f;

    /// <summary>卡框上边缘。</summary>
    private const float CardTop = -211f;

    /// <summary>卡框下边缘。</summary>
    private const float CardBottom = 211f;

    /// <summary>单枚书签高度。</summary>
    private const float BookmarkThickness = 20f;

    /// <summary>枚与枚之间的竖直缝隙。</summary>
    private const float BookmarkGap = 4f;

    /// <summary>左端压入卡框内侧的像素数。</summary>
    private const float BookmarkInset = 4f;

    /// <summary>矩形段右端（相对卡框右边缘外凸 6px）。</summary>
    private const float BookmarkBodyRight = CardRight + 6f;

    /// <summary>箭头尖端 x（相对卡框右边缘外凸 16px）。</summary>
    private const float BookmarkTipRight = CardRight + 16f;

    /// <summary>整组书签在卡框右边缘上的垂直中心（= 卡框竖直中心 y=0）。</summary>
    private const float GroupCenterY = (CardTop + CardBottom) * 0.5f;

    /// <summary>组顶硬下限：任何情况下都不许把书签推到标题横幅的高度（横幅占 y ∈ [−207, −124]）。</summary>
    private const float MinGroupTopY = -160f;

    /// <summary>描边宽度（卡面像素）。</summary>
    private const float OutlineWidth = 1.5f;

    /// <summary>单枚书签在卡面坐标系里的包围盒（以组顶 y=0 为基准，贴图 UV 按它归一化）。</summary>
    public static readonly Rect2 BookmarkCanvasInCard = new(
        CardRight - BookmarkInset,
        0f,
        BookmarkTipRight - (CardRight - BookmarkInset),
        BookmarkThickness);

    private Texture2D? _bookmarkTexture;
    private bool _bookmarkTextureLoaded;

    /// <summary>
    /// 为指定卡牌创建书签节点；无和弦时返回仍不可见的空节点（由 <see cref="Refresh(CardModel?)" /> 决定是否显示）。
    /// </summary>
    public static NChordBookmark Create(CardModel card)
    {
        var bookmark = CreateEmpty();
        bookmark.Refresh(card);
        return bookmark;
    }

    /// <summary>
    /// 按一个显式和弦掩码创建书签节点，用于不修改卡牌数据的结果预览（例如和弦设计面板的结果展示）。
    /// </summary>
    public static NChordBookmark Create(PerformChord chord)
    {
        var bookmark = CreateEmpty();
        bookmark.Refresh(chord);
        return bookmark;
    }

    private static NChordBookmark CreateEmpty() => new()
    {
        Name = NodeName,
        // 覆盖层只负责显示，绝不能吃掉卡牌的鼠标事件（悬停 / 点击 / 拖拽都归 NCard）
        MouseFilter = MouseFilterEnum.Ignore
    };

    /// <summary>
    /// 按和弦数量算出每枚书签的矩形段矩形（卡面坐标系）。返回顺序即自上而下的堆叠顺序，
    /// 与 <see cref="PerformChordExtensions.EnumerateGroups" /> 给出的和弦顺序一一对应。
    /// </summary>
    public static Rect2[] BuildBookmarkRects(int count)
    {
        if (count <= 0)
        {
            return [];
        }

        var totalHeight = count * BookmarkThickness + (count - 1) * BookmarkGap;
        var top = GroupCenterY - totalHeight * 0.5f;
        if (top < MinGroupTopY)
        {
            top = MinGroupTopY;
        }

        var rects = new Rect2[count];
        for (var index = 0; index < count; index++)
        {
            rects[index] = new Rect2(
                BookmarkCanvasInCard.Position.X,
                top + index * (BookmarkThickness + BookmarkGap),
                BookmarkCanvasInCard.Size.X,
                BookmarkThickness);
        }

        return rects;
    }

    /// <summary>
    /// 把一枚书签的矩形段扩成五边形：矩形段（左上 → 右上 → 右下 → 左下）+ 右端 <c>&gt;</c> 箭头尖端。
    /// 顶点顺序固定为 <c>左上、右上、尖端、右下、左下</c>，供 <see cref="Polygon2D" /> 与预览脚本共用。
    /// </summary>
    public static Vector2[] BuildBookmarkShape(Rect2 rect)
    {
        var left = rect.Position.X;
        var right = BookmarkBodyRight;
        var middle = rect.Position.Y + rect.Size.Y * 0.5f;
        return
        [
            new Vector2(left, rect.Position.Y),
            new Vector2(right, rect.Position.Y),
            new Vector2(BookmarkTipRight, middle),
            new Vector2(right, rect.End.Y),
            new Vector2(left, rect.End.Y)
        ];
    }

    /// <summary>按卡牌当前的和弦重建书签；无和弦时隐藏。</summary>
    public void Refresh(CardModel? card)
    {
        var chord = card != null && card.TryGetCapability<ChordCapability>(out var capability)
            ? capability.Chord
            : PerformChord.None;
        Refresh(chord);
    }

    /// <summary>按显式和弦掩码重建书签；无和弦时隐藏。不读写卡牌能力，可用于结果预览。</summary>
    public void Refresh(PerformChord chord)
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        var groups = chord.EnumerateGroups().ToArray();
        if (groups.Length == 0)
        {
            Visible = false;
            return;
        }

        Visible = true;
        var rects = BuildBookmarkRects(groups.Length);
        for (var index = 0; index < groups.Length && index < rects.Length; index++)
        {
            AddChild(CreateBookmark(rects[index], PerformChordColors.ColorOf(groups[index])));
        }
    }

    /// <summary>
    /// 创建一枚书签：有贴图时按五边形裁切并染色，没有贴图时退回纯色多边形；两者都描一条加深的外边。
    /// </summary>
    private CanvasItem CreateBookmark(Rect2 rect, Color color)
    {
        var shape = BuildBookmarkShape(rect);
        var texture = GetBookmarkTexture();

        var holder = new Node2D();
        var polygon = new Polygon2D
        {
            Polygon = shape,
            Color = color,
            Texture = texture
        };

        if (texture != null)
        {
            // 顶点是卡面坐标，先平移成「相对单枚书签画布」的坐标，再乘贴图倍率
            var uvs = new Vector2[shape.Length];
            for (var i = 0; i < shape.Length; i++)
            {
                uvs[i] = (shape[i] - new Vector2(BookmarkCanvasInCard.Position.X, rect.Position.Y)) * TextureScale;
            }

            polygon.UV = uvs;
        }

        holder.AddChild(polygon);

        // 闭合描边：Line2D 没有 closed 开关，把首点追加到末尾即为闭合环
        var outlinePoints = new Vector2[shape.Length + 1];
        shape.CopyTo(outlinePoints, 0);
        outlinePoints[^1] = shape[0];

        var outlineColor = color.Darkened(0.55f);
        outlineColor.A = 0.85f;
        holder.AddChild(new Line2D
        {
            Points = outlinePoints,
            Width = OutlineWidth,
            DefaultColor = outlineColor,
            JointMode = Line2D.LineJointMode.Round,
            Antialiased = true
        });

        return holder;
    }

    /// <summary>懒加载书签贴图；文件缺失时返回 <see langword="null" /> 让调用方走纯色多边形分支。</summary>
    private Texture2D? GetBookmarkTexture()
    {
        if (_bookmarkTextureLoaded)
        {
            return _bookmarkTexture;
        }

        _bookmarkTextureLoaded = true;
        if (ResourceLoader.Exists(TexturePath))
        {
            _bookmarkTexture = GD.Load<Texture2D>(TexturePath);
        }

        return _bookmarkTexture;
    }
}
