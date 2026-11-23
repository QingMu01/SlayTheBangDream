namespace BangDreamInSpire.Scripts.Enums;

/// <summary>和弦位掩码，可同时标记多个奏响分组。</summary>
/// <remarks>位值直接用于存档和网络 payload，不得重排、复用或删除；新增只能追加空位。</remarks>
[Flags]
public enum PerformChord
{
    None = 0,

    C = 1 << 0,

    D = 1 << 1,

    F = 1 << 2,

    G = 1 << 3,

    All = C | D | F | G
}

/// <summary>和弦位掩码辅助。</summary>
public static class PerformChordExtensions
{
    /// <summary>单和弦枚举与优先级顺序。</summary>
    private static readonly PerformChord[] Groups = [PerformChord.C, PerformChord.D, PerformChord.F, PerformChord.G];

    /// <summary>按 C、D、F、G 顺序枚举有效位。</summary>
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

    /// <summary>返回优先级最高的单和弦，空掩码返回 None。</summary>
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

    /// <summary>是否恰好包含一个有效和弦。</summary>
    public static bool IsSingleGroup(this PerformChord chord)
    {
        return chord != PerformChord.None && ((int)chord & ((int)chord - 1)) == 0;
    }
}
