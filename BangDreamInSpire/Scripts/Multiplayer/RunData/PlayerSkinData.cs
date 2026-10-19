using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;

namespace BangDreamInSpire.Scripts.Multiplayer.RunData;

public class PlayerSkinData
{
    public string? SkinPath { get; set; }

    public SkinInfo? GetSkin()
    {
        return SkinPath != null ? SkinManager.GetSkinInfo(SkinPath) : null;
    }
}