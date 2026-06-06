using BangDreamLib.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamLib.Scripts.Extensions;

public static class BangDreamBandExtensions
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
