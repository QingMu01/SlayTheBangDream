using BangDreamInSpire.Scripts.Enums;
using Godot;

namespace BangDreamInSpire.Scripts.Mechanics.Perform;

/// <summary>
/// 演奏和弦的配色表：卡牌角标与演奏区槽位共用同一份，避免两处颜色各写一遍而漂移。
/// <para>
/// 每个和弦对应一名队友的角色色（C→祥子、D→素世、F→灯、G→立希）。核心模组不能引用角色模组，
/// 因此颜色值以本表为唯一来源，角色模组的 <c>CrychicMemberEnum</c> 反向引用本表。
/// </para>
/// </summary>
public static class PerformChordColors
{
    private static readonly Dictionary<PerformChord, Color> Colors = new()
    {
        [PerformChord.C] = new("#7799CC"), // 祥子
        [PerformChord.D] = new("#FFDD88"), // 素世
        [PerformChord.F] = new("#77BBDD"), // 灯
        [PerformChord.G] = new("#7777AA") // 立希
    };

    /// <summary>无和弦、多和弦掩码或未知和弦时的回退色。</summary>
    public static readonly Color Fallback = new("#9d9d9d");

    /// <summary>取单个和弦的配色；<see cref="PerformChord.None" /> 或复合掩码返回 <see cref="Fallback" />。</summary>
    public static Color ColorOf(PerformChord chord)
    {
        return Colors.TryGetValue(chord, out var color) ? color : Fallback;
    }
}
