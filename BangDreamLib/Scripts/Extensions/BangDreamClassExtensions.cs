using BangDreamLib.Scripts.Utils.Infos;

namespace BangDreamLib.Scripts.Extensions;

public static class BangDreamClassExtensions
{
    private static readonly Dictionary<BangDreamClass, string> BandClassInfo = new()
    {
        [BangDreamClass.Keyboard] = "Key.",
        [BangDreamClass.Vocal] = "Vol.",
        [BangDreamClass.Drum] = "Dr.",
        [BangDreamClass.Guitar] = "Gt.",
        [BangDreamClass.Bass] = "Bs.",
        [BangDreamClass.Dj] = "Dj.",
        [BangDreamClass.Violin] = "Vn."
    };

    public static string GetBandClass(this BangDreamClass bandClass)
    {
        var result = new List<string>();

        foreach (BangDreamClass value in Enum.GetValues(typeof(BangDreamClass)))
        {
            if (IsPowerOfTwo((int)value) && bandClass.HasFlag(value))
            {
                if (BandClassInfo.TryGetValue(value, out var name))
                {
                    result.Add(name);
                }
            }
        }

        return string.Join(" & ", result);
    }

    private static bool IsPowerOfTwo(int value)
    {
        return value > 0 && (value & (value - 1)) == 0;
    }
}
