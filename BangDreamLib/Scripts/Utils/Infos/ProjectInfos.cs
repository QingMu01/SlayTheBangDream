using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamLib.Scripts.Utils.Infos;

public enum BangDreamBand
{
    PoppinParty,
    Afterglow,
    PastelPalettes,
    Roselia,
    HelloHappyWorld,
    Morfonica,
    RaiseASuilen,
    MyGo,
    AveMujica,
    YumemitaMewType,
    Millsage,
    IkaDumbRock,
    Crychic
}

[Flags]
public enum BangDreamClass
{
    Keyboard = 1,
    Vocal = 2,
    Drum = 4,
    Guitar = 8,
    Bass = 16,
    Dj = 32,
    Violin = 64
}

public enum BangDreamMember
{
    Sakiko,
    Mutsumi,
    Tomori,
    Soyo,
    Taki
}

public record BandInfo(string Name, Color Color);

public record MemberInfo(string Name, string NameRoman, Color Color);

public static class BandEnumExtensions
{
    private static readonly Dictionary<BangDreamBand, BandInfo> BandInfo = new()
    {
        [BangDreamBand.PoppinParty] = new BandInfo("Poppin'Party", new Color("#FF3377")),
        [BangDreamBand.Afterglow] = new BandInfo("Afterglow", new Color("#EE3344")),
        [BangDreamBand.PastelPalettes] = new BandInfo("Pastel*Palettes", new Color("#33DDAA")),
        [BangDreamBand.Roselia] = new BandInfo("Roselia", new Color("#3344AA")),
        [BangDreamBand.HelloHappyWorld] = new BandInfo("Hello,Happy World!", new Color("#FFDD00")),
        [BangDreamBand.Morfonica] = new BandInfo("Morfonica", new Color("#33AAFF")),
        [BangDreamBand.RaiseASuilen] = new BandInfo("RAISE A SUILEN", new Color("#33CCCC")),
        [BangDreamBand.MyGo] = new BandInfo("MyGO!!!!!", new Color("#3388BB")),
        [BangDreamBand.AveMujica] = new BandInfo("Ave Mujica", new Color("#881144")),
        [BangDreamBand.YumemitaMewType] = new BandInfo("Yumemita MewType", new Color("#FF7788")),
        [BangDreamBand.Millsage] = new BandInfo("Millsage", new Color("#AA22EE")),
        [BangDreamBand.IkaDumbRock] = new BandInfo("Ika Dumb Rock!", new Color("#FFAA33")),
        [BangDreamBand.Crychic] = new BandInfo("Crychic", new Color("#8CD3D4"))
    };

    private static readonly Dictionary<BangDreamBand, (string, string)> BandSelectIcon = new();

    public static BandInfo GetBandInfo(this BangDreamBand band)
    {
        return BandInfo[band];
    }

    public static string GetBandName(this BangDreamBand band)
    {
        return BandInfo[band].Name;
    }

    public static Color GetBandColor(this BangDreamBand band)
    {
        return BandInfo[band].Color;
    }

    public static string GetSelectIcon(this BangDreamBand band)
    {
        return BandSelectIcon.TryGetValue(band, out var icon)
            ? icon.Item1
            : ImageHelper.GetImagePath("packed/character_select/char_select_random.png");
    }

    public static string GetSelectIconLocked(this BangDreamBand band)
    {
        return BandSelectIcon.TryGetValue(band, out var icon)
            ? icon.Item2
            : ImageHelper.GetImagePath("packed/character_select/char_select_random_locked.png");
    }

    public static BangDreamBand SetBandSelectIcon(this BangDreamBand band, string iconPath,
        string iconLockedPath)
    {
        BandSelectIcon[band] = (iconPath, iconLockedPath);
        return band;
    }
}

public static class BangDreamBandClassExtensions
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

public static class MemberEnumExtensions
{
    private static readonly Dictionary<BangDreamMember, MemberInfo> MemberInfo = new()
    {
        [BangDreamMember.Sakiko] = new MemberInfo("TogawaSakiko", "Sakiko Togawa", new Color("#7799CC")),
        [BangDreamMember.Mutsumi] = new MemberInfo("WakabaMutsumi", "Mutsumi Wakaba", new Color("#779977")),
        [BangDreamMember.Tomori] = new MemberInfo("TakamatsuTomori", "Tomori Takamatsu", new Color("#77BBDD")),
        [BangDreamMember.Soyo] = new MemberInfo("NagasakiSoyo", "Soyo Nagasaki", new Color("#FFDD88")),
        [BangDreamMember.Taki] = new MemberInfo("ShiinaTaki", "Taki Shiina", new Color("#7777AA"))
    };

    public static MemberInfo GetMemberInfo(this BangDreamMember member)
    {
        return MemberInfo[member];
    }

    public static string GetMemberName(this BangDreamMember member)
    {
        return MemberInfo[member].Name;
    }

    public static string GetMemberNameRoman(this BangDreamMember member)
    {
        return MemberInfo[member].NameRoman;
    }

    public static Color GetMemberColor(this BangDreamMember member)
    {
        return MemberInfo[member].Color;
    }
}