using BangDreamInSpire.Scripts.Enums;
using Godot;

namespace BangDreamInSpire.Scripts.Utils.Infos;

/// <summary>书签与槽位共用的和弦配色，内容模组反向引用本表。</summary>
public static class PerformChordColors
{
    private static readonly Dictionary<PerformChord, Color> Colors = new()
    {
        [PerformChord.C] = new("#7799CC"), // 祥子
        [PerformChord.D] = new("#FFDD88"), // 素世
        [PerformChord.F] = new("#77BBDD"), // 灯
        [PerformChord.G] = new("#7777AA") // 立希
    };

    /// <summary>无法确定单和弦时的回退色。</summary>
    public static readonly Color Fallback = new("#9d9d9d");

    /// <summary>返回单和弦配色，其他情况使用回退色。</summary>
    public static Color ColorOf(PerformChord chord)
    {
        return Colors.TryGetValue(chord, out var color) ? color : Fallback;
    }
}
