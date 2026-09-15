namespace BangDreamLib.Scripts.Enums;

/// <summary>
/// 演奏和弦。以位掩码表达，一张卡牌可同时带有多个和弦。
/// </summary>
[Flags]
public enum PerformChord
{
    /// <summary>
    /// 无和弦。
    /// </summary>
    None = 0,

    /// <summary>
    /// C 和弦。
    /// </summary>
    C = 1 << 0,

    /// <summary>
    /// D 和弦。
    /// </summary>
    D = 1 << 1,

    /// <summary>
    /// F 和弦。
    /// </summary>
    F = 1 << 2,

    /// <summary>
    /// G 和弦。
    /// </summary>
    G = 1 << 3,

    /// <summary>
    /// 全部和弦。
    /// </summary>
    All = C | D | F | G
}

/// <summary>
/// <see cref="PerformChord" /> 的位掩码辅助。
/// </summary>
public static class PerformChordExtensions
{
    /// <summary>单和弦枚举顺序，同时用作"首个和弦"的优先级。</summary>
    private static readonly PerformChord[] Groups = [PerformChord.C, PerformChord.D, PerformChord.F, PerformChord.G];

    /// <summary>
    /// 按 <see cref="PerformChord.C" />、<see cref="PerformChord.D" />、<see cref="PerformChord.F" />、
    /// <see cref="PerformChord.G" /> 的顺序逐位枚举所含和弦，忽略未定义位。
    /// </summary>
    public static IEnumerable<PerformChord> EnumerateGroups(this PerformChord chord)
    {
        foreach (var group in Groups)
        {
            if (chord.HasFlag(group))
            {
                yield return group;
            }
        }
    }

    /// <summary>
    /// 取掩码中优先级最高的单和弦。多和弦卡牌以它决定入队分组，无和弦时返回
    /// <see cref="PerformChord.None" />。
    /// </summary>
    public static PerformChord FirstGroup(this PerformChord chord)
    {
        foreach (var group in Groups)
        {
            if (chord.HasFlag(group))
            {
                return group;
            }
        }

        return PerformChord.None;
    }

    /// <summary>是否为恰好一个已定义的和弦。</summary>
    public static bool IsSingleGroup(this PerformChord chord)
    {
        return chord != PerformChord.None && ((int)chord & ((int)chord - 1)) == 0;
    }
}
