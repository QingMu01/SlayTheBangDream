using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;

namespace BangDreamLib.Scripts.Multiplayer.RunData;

public class PlayerSkinData
{
    public string? SkinPath { get; set; }

    public SkinInfo? GetSkin()
    {
        return SkinPath != null ? SkinManager.GetSkinInfo(SkinPath) : null;
    }
}