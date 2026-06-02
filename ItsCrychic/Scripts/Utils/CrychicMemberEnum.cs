using BangDreamLib.Scripts.Utils.Infos;
using Godot;

namespace ItsCrychic.Scripts.Utils;

public enum CrychicMemberEnum
{
    Sakiko,
    Mutsumi,
    Tomori,
    Soyo,
    Taki
}

public static class MemberEnumExtensions
{
    private static readonly Dictionary<CrychicMemberEnum, MemberInfo> MemberInfo = new()
    {
        [CrychicMemberEnum.Sakiko] = new MemberInfo("TogawaSakiko", "Sakiko Togawa", new Color("#7799CC")),
        [CrychicMemberEnum.Mutsumi] = new MemberInfo("WakabaMutsumi", "Mutsumi Wakaba", new Color("#779977")),
        [CrychicMemberEnum.Tomori] = new MemberInfo("TakamatsuTomori", "Tomori Takamatsu", new Color("#77BBDD")),
        [CrychicMemberEnum.Soyo] = new MemberInfo("NagasakiSoyo", "Soyo Nagasaki", new Color("#FFDD88")),
        [CrychicMemberEnum.Taki] = new MemberInfo("ShiinaTaki", "Taki Shiina", new Color("#7777AA"))
    };

    public static MemberInfo GetMemberInfo(this CrychicMemberEnum memberEnum)
    {
        return MemberInfo[memberEnum];
    }

    public static string GetMemberName(this CrychicMemberEnum memberEnum)
    {
        return MemberInfo[memberEnum].Name;
    }

    public static string GetMemberNameRoman(this CrychicMemberEnum memberEnum)
    {
        return MemberInfo[memberEnum].NameRoman;
    }

    public static Color GetMemberColor(this CrychicMemberEnum memberEnum)
    {
        return MemberInfo[memberEnum].Color;
    }
}