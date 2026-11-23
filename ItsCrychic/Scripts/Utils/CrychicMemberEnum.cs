using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Utils.Infos;
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
    /// <summary>角色配色；和弦色统一来自 <see cref="PerformChordColors" />。</summary>
    private static readonly Dictionary<CrychicMemberEnum, MemberInfo> MemberInfo = new()
    {
        [CrychicMemberEnum.Sakiko] = new MemberInfo("TogawaSakiko", "Sakiko Togawa",
            PerformChordColors.ColorOf(PerformChord.C)),
        [CrychicMemberEnum.Mutsumi] = new MemberInfo("WakabaMutsumi", "Mutsumi Wakaba", new Color("#779977")),
        [CrychicMemberEnum.Tomori] = new MemberInfo("TakamatsuTomori", "Tomori Takamatsu",
            PerformChordColors.ColorOf(PerformChord.F)),
        [CrychicMemberEnum.Soyo] = new MemberInfo("NagasakiSoyo", "Soyo Nagasaki",
            PerformChordColors.ColorOf(PerformChord.D)),
        [CrychicMemberEnum.Taki] = new MemberInfo("ShiinaTaki", "Taki Shiina",
            PerformChordColors.ColorOf(PerformChord.G))
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