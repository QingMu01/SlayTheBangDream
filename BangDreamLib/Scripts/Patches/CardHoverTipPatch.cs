using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using STS2RitsuLib.Patching.Models;

namespace BangDreamLib.Scripts.Patches;

public class CardHoverTipPatch : IPatchMethod
{
    private const float CardPadding = 4f;
    private const float ViewportMargin = 16f;

    public static string PatchId => "batter_card_hover_tips_container_layout";
    
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(NHoverTipCardContainer), nameof(NHoverTipCardContainer.LayoutResizeAndReposition))
        ];
    }

    public static bool Prefix(NHoverTipCardContainer __instance, Vector2 globalStartLocation,
        HoverTipAlignment alignment)
    {
        var tips = __instance.GetChildren().OfType<Control>().ToList();
        if (tips.Count == 1)
        {
            __instance.Scale = Vector2.One;
            return true;
        }

        if (tips.Count == 0)
        {
            __instance.Scale = Vector2.One;
            __instance.Size = Vector2.Zero;
            __instance.GlobalPosition = globalStartLocation;
            return false;
        }

        var viewportRect = __instance.GetViewportRect();
        var viewportMin = viewportRect.Position + Vector2.One * ViewportMargin;
        var viewportMax = viewportRect.Position + viewportRect.Size - Vector2.One * ViewportMargin;
        var availableSize = new Vector2(
            Mathf.Max(1f, alignment == HoverTipAlignment.Left
                ? globalStartLocation.X - viewportMin.X
                : viewportMax.X - globalStartLocation.X),
            Mathf.Max(1f, viewportMax.Y - globalStartLocation.Y)
        );

        var columns = BuildSquareGrid(tips);
        var columnWidths = columns
            .Select(column => column.Max(tip => tip.Size.X))
            .ToList();
        var rowCount = columns.Max(column => column.Count);
        var rowHeights = Enumerable.Range(0, rowCount)
            .Select(rowIndex => columns
                .Where(column => column.Count > rowIndex)
                .Max(column => column[rowIndex].Size.Y))
            .ToList();

        var contentSize = new Vector2(
            columnWidths.Sum() + CardPadding * (columns.Count - 1),
            rowHeights.Sum() + CardPadding * (rowCount - 1)
        );

        LayoutGrid(columns, columnWidths, rowHeights, contentSize.X, alignment);

        var scale = Mathf.Min(
            1f,
            Mathf.Min(
                availableSize.X / Mathf.Max(1f, contentSize.X),
                availableSize.Y / Mathf.Max(1f, contentSize.Y)
            )
        );
        __instance.Scale = Vector2.One * scale;
        __instance.Size = contentSize;

        var scaledSize = contentSize * scale;
        __instance.GlobalPosition = new Vector2(
            alignment == HoverTipAlignment.Left
                ? globalStartLocation.X - scaledSize.X
                : globalStartLocation.X,
            globalStartLocation.Y
        );

        return false;
    }

    private static List<List<Control>> BuildSquareGrid(IReadOnlyList<Control> tips)
    {
        var columnCount = Mathf.CeilToInt(Mathf.Sqrt(tips.Count));
        var rowCount = Mathf.CeilToInt(tips.Count / (float)columnCount);
        var columns = Enumerable.Range(0, columnCount)
            .Select(_ => new List<Control>())
            .ToList();

        for (var index = 0; index < tips.Count; index++)
        {
            columns[index / rowCount].Add(tips[index]);
        }

        return columns;
    }

    private static void LayoutGrid(IReadOnlyList<List<Control>> columns, IReadOnlyList<float> columnWidths,
        IReadOnlyList<float> rowHeights, float contentWidth, HoverTipAlignment alignment)
    {
        var rowOffsets = new float[rowHeights.Count];
        for (var rowIndex = 1; rowIndex < rowOffsets.Length; rowIndex++)
        {
            rowOffsets[rowIndex] = rowOffsets[rowIndex - 1] + rowHeights[rowIndex - 1] + CardPadding;
        }

        var columnX = alignment == HoverTipAlignment.Left ? contentWidth : 0f;

        for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
            var columnWidth = columnWidths[columnIndex];
            if (alignment == HoverTipAlignment.Left)
            {
                columnX -= columnWidth;
            }

            for (var rowIndex = 0; rowIndex < columns[columnIndex].Count; rowIndex++)
            {
                columns[columnIndex][rowIndex].Position = new Vector2(columnX, rowOffsets[rowIndex]);
            }

            columnX += alignment == HoverTipAlignment.Left
                ? -CardPadding
                : columnWidth + CardPadding;
        }
    }
}
