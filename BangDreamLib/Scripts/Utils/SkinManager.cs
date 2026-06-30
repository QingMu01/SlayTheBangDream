using BangDreamLib.Scripts.Utils.Infos;

namespace BangDreamLib.Scripts.Utils;

public class SkinManager
{
    private static readonly Dictionary<string, SkinInfo> SkinsMap = new();

    public static void RegisterCharacterSkin(string path, SkinTemplate skinTemplates)
    {
        if (SkinsMap.ContainsKey(path))
        {
            BangDreamLibCore.Logger.Warn($"Skin({path}) already exists, will be overwrite it.");
        }

        SkinsMap[path] = new SkinInfo(skinTemplates);
    }

    public static SkinInfo? GetSkinInfo(string path)
    {
        return SkinsMap.GetValueOrDefault(path);
    }
}