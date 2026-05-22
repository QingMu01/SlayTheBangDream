using Godot;

namespace BangDreamLib.Scripts.Extensions;

public static class ResPathExtensions
{
    public static string? GetCardImg(this string name, string resourceFolder = "Resource")
    {
        var fullPath = $"{resourceFolder}/images/card_portraits/{name}.png";
        return ResourceLoader.Exists(fullPath) ? fullPath : null;
    }

    public static string? GetCardBateImg(this string name, string resourceFolder = "Resource")
    {
        var fullPath = $"{resourceFolder}/images/card_portraits/{name}_Bate.png";
        return ResourceLoader.Exists(fullPath) ? fullPath : null;
    }

    public static string? GetBigRelicImg(this string name, string resourceFolder = "Resource")
    {
        var fullPath = $"{resourceFolder}/images/relics/big/{name}.png";
        return ResourceLoader.Exists(fullPath) ? fullPath : null;
    }

    public static string? GetRelicImg(this string name, string resourceFolder = "Resource")
    {
        var fullPath = $"{resourceFolder}/images/relics/{name}.png";
        return ResourceLoader.Exists(fullPath) ? fullPath : null;
    }

    public static string? EmptyStringFilter(this string str)
    {
        return string.IsNullOrEmpty(str) ? null : str;
    }
}