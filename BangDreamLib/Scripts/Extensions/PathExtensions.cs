using Godot;

namespace BangDreamLib.Scripts.Extensions;

public static class PathExtensions
{
    private const string DefaultCardPortrait = "res://BangDreamLib/images/sceneui/default_portrait.png";

    public static string GetCardImg(this Type type, string? resourceFolder = null)
    {
        var folder = resourceFolder ?? type.Namespace?.Split(".")[0] ?? throw new FileNotFoundException("No valid folder!");
        var filename = type.Name;
        var fullPath = $"{folder}/images/card_portraits/{filename}.png";
        return ResourceLoader.Exists(fullPath) ? fullPath : DefaultCardPortrait;
    }

    public static string? GetCardBateImg(this Type type, string? resourceFolder = null)
    {
        var folder = resourceFolder ?? type.Namespace?.Split(".")[0] ?? throw new FileNotFoundException("No valid folder!");
        var filename = type.Name;
        var fullPath = $"{folder}/images/card_portraits/{filename}_Bate.png";
        return ResourceLoader.Exists(fullPath) ? fullPath : null;
    }

    public static string? GetBigRelicImg(this Type type, string? resourceFolder = null)
    {
        var folder = resourceFolder ?? type.Namespace?.Split(".")[0] ?? throw new FileNotFoundException("No valid folder!");
        var filename = type.Name;
        var fullPath = $"{folder}/images/relics/big/{filename}.png";
        return ResourceLoader.Exists(fullPath) ? fullPath : null;
    }

    public static string? GetRelicImg(this Type type, string? resourceFolder = null)
    {
        var folder = resourceFolder ?? type.Namespace?.Split(".")[0] ?? throw new FileNotFoundException("No valid folder!");
        var filename = type.Name;
        var fullPath = $"{folder}/images/relics/{filename}.png";
        return ResourceLoader.Exists(fullPath) ? fullPath : null;
    }

    public static string? EmptyStringFilter(this string str)
    {
        return string.IsNullOrEmpty(str) ? null : str;
    }
}