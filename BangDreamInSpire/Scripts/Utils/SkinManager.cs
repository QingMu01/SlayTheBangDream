using BangDreamInSpire.Scripts.Utils.Infos;

namespace BangDreamInSpire.Scripts.Utils;

public static class SkinManager
{
    private static readonly Dictionary<string, SkinInfo> SkinsMap = new();

    public static void RegisterCharacterSkin(string path, SkinTemplate skinTemplates)
    {
        if (SkinsMap.ContainsKey(path))
        {
            BangDreamInSpireCore.Logger.Warn($"Skin({path}) already exists, will be overwrite it.");
        }

        SkinsMap[path] = new SkinInfo(skinTemplates);
    }

    public static SkinInfo? GetSkinInfo(string path)
    {
        return SkinsMap.GetValueOrDefault(path);
    }
}