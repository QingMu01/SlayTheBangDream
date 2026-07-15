using BangDreamLib.Scripts.Enums;
using BangDreamLib.Scripts.Utils.Infos;
using Godot;

namespace BangDreamLib.Scripts.Extensions;

public static class CharacterGroupExtensions
{
    private static readonly Lock AsyncLock = new();

    /// <summary>
    /// 乐队成员选择页默认配置
    /// </summary>
    private static readonly Dictionary<CharacterGroup, GroupInfo> BandInfo = new()
    {
        [CharacterGroup.PoppinParty] = new GroupInfo("Poppin'Party", new Color("#FF3377")),
        [CharacterGroup.Afterglow] = new GroupInfo("Afterglow", new Color("#EE3344")),
        [CharacterGroup.PastelPalettes] = new GroupInfo("Pastel*Palettes", new Color("#33DDAA")),
        [CharacterGroup.Roselia] = new GroupInfo("Roselia", new Color("#3344AA")),
        [CharacterGroup.HelloHappyWorld] = new GroupInfo("Hello,Happy World!", new Color("#FFDD00")),
        [CharacterGroup.Morfonica] = new GroupInfo("Morfonica", new Color("#33AAFF")),
        [CharacterGroup.RaiseASuilen] = new GroupInfo("RAISE A SUILEN", new Color("#33CCCC")),
        [CharacterGroup.MyGo] = new GroupInfo("MyGO!!!!!", new Color("#3388BB")),
        [CharacterGroup.AveMujica] = new GroupInfo("Ave Mujica", new Color("#881144")),
        [CharacterGroup.YumemitaMewType] = new GroupInfo("Yumemita MewType", new Color("#FF7788")),
        [CharacterGroup.Millsage] = new GroupInfo("Millsage", new Color("#AA22EE")),
        [CharacterGroup.IkaDumbRock] = new GroupInfo("Ika Dumb Rock!", new Color("#FFAA33")),
        [CharacterGroup.Crychic] = new GroupInfo("Crychic", new Color("#8CD3D4"))
    };

    /// <summary>
    /// 乐队分组选择按钮图标
    /// string: iconPath, string: iconLockedPath
    /// </summary>
    private static readonly Dictionary<CharacterGroup, string> BandSelectIcon = new();


    /// <summary>
    /// 获取分组信息，在角色选择页确定主题颜色
    /// </summary>
    public static GroupInfo GetGroupInfo(this CharacterGroup group)
    {
        return BandInfo.TryGetValue(group, out var info)
            ? info
            : throw new ArgumentException($"Unknow character group: {group}.");
    }

    /// <summary>
    /// 获取分组选择按钮图标
    /// </summary>
    public static string? GetGroupSelectIcon(this CharacterGroup group)
    {
        lock (AsyncLock)
        {
            return BandSelectIcon.GetValueOrDefault(group);
        }
    }
    
    /// <summary>
    /// 设置分组选择按钮图标
    /// </summary>
    /// <param name="group">分组枚举</param>
    /// <param name="iconPath">图标路径</param>
    public static void SetBandSelectIcon(this CharacterGroup group, string iconPath)
    {
        lock (AsyncLock)
        {
            if (!BandSelectIcon.TryAdd(group, iconPath))
            {
                BangDreamLibCore.Logger.Warn(
                    $"Character group: {group} already exists Icons. Will be overwritten.");
                BandSelectIcon[group] = iconPath;
            }
            else
            {
                BangDreamLibCore.Logger.Info($"Set group select icons: {group} .");
            }
        }
    }
}