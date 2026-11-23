using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Mechanics.Perform;
using BangDreamInSpire.Scripts.Utils.Infos;
using BangDreamInSpire.Scripts.Mechanics.Perform.Chord;
using Godot;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Models.Capabilities;

namespace BangDreamInSpire.Scripts.Nodes.Cards;

/// <summary>卡框右缘的和弦书签，按 C→D→F→G 垂直居中堆叠。
/// 几何使用 CardContainer 坐标，组顶不得越过 MinGroupTopY 进入标题区。
/// 颜色来自 PerformChordColors；贴图缺失时使用同形状纯色多边形。
/// OverlayContainer 保持位于标题层之下，不得提高 ZIndex。</summary>
public partial class NChordBookmark : Control
{
    /// <summary>可选贴图，按单枚书签画布导出；缺失时使用同形状纯色多边形。</summary>
    public const string TexturePath = "res://BangDreamInSpire/images/sceneui/chord_bookmark.png";

    public const string NodeName = "ChordBookmark";

    /// <summary>贴图相对卡面像素的倍率。</summary>
    private const float TextureScale = 2f;

    private const float CardRight = 150f;

    private const float CardTop = -211f;

    private const float CardBottom = 211f;

    private const float BookmarkThickness = 20f;

    private const float BookmarkGap = 4f;

    private const float BookmarkInset = 4f;

    private const float BookmarkBodyRight = CardRight + 6f;

    private const float BookmarkTipRight = CardRight + 16f;

    private const float GroupCenterY = (CardTop + CardBottom) * 0.5f;

    /// <summary>组顶不得进入标题横幅。</summary>
    private const float MinGroupTopY = -160f;

    private const float OutlineWidth = 1.5f;

    /// <summary>单枚书签包围盒，用于贴图 UV。</summary>
    public static readonly Rect2 BookmarkCanvasInCard = new(
        CardRight - BookmarkInset,
        0f,
        BookmarkTipRight - (CardRight - BookmarkInset),
        BookmarkThickness);

    private Texture2D? _bookmarkTexture;
    private bool _bookmarkTextureLoaded;

    /// <summary>按卡牌和弦创建书签。</summary>
    public static NChordBookmark Create(CardModel card)
    {
        var bookmark = CreateEmpty();
        bookmark.Refresh(card);
        return bookmark;
    }

    /// <summary>按显式掩码创建预览书签，不修改卡牌。</summary>
    public static NChordBookmark Create(PerformChord chord)
    {
        var bookmark = CreateEmpty();
        bookmark.Refresh(chord);
        return bookmark;
    }

    private static NChordBookmark CreateEmpty() => new()
    {
        Name = NodeName,
        // 鼠标事件全部留给 NCard。
        MouseFilter = MouseFilterEnum.Ignore
    };

    /// <summary>按堆叠顺序生成矩形段。</summary>
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

    /// <summary>生成固定顶点顺序的箭头五边形。</summary>
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

    /// <summary>按卡牌当前和弦重建书签。</summary>
    public void Refresh(CardModel? card)
    {
        var chord = card != null && card.TryGetCapability<ChordCapability>(out var capability)
            ? capability.Chord
            : PerformChord.None;
        Refresh(chord);
    }

    /// <summary>按显式掩码重建预览书签。</summary>
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

    /// <summary>创建贴图或纯色书签并描边。</summary>
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
            // 顶点转换到单枚画布并应用贴图倍率。
            var uvs = new Vector2[shape.Length];
            for (var i = 0; i < shape.Length; i++)
            {
                uvs[i] = (shape[i] - new Vector2(BookmarkCanvasInCard.Position.X, rect.Position.Y)) * TextureScale;
            }

            polygon.UV = uvs;
        }

        holder.AddChild(polygon);

        // Line2D 通过重复首点闭合。
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

    /// <summary>懒加载贴图，缺失返回 null。</summary>
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
