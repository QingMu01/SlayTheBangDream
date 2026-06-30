using BangDreamLib.Scripts.Multiplayer.RunData;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;

namespace BangDreamLib.Scripts.Extensions;

public static class SkinDataExtensions
{
    public static SkinInfo? GetSkin(this PlayerSkinData skinData)
    {
        if (skinData.SkinPath != null)
        {
            SkinManager.GetSkinInfo(skinData.SkinPath);
        }

        return null;
    }
}