using BangDreamInSpire.Scripts.Utils;

namespace BangDreamInSpire.Scripts.Utils.Infos;

public class PlayerSkinData
{
    public string? SkinPath { get; set; }

    public SkinInfo? GetSkin()
    {
        return SkinPath != null ? SkinManager.GetSkinInfo(SkinPath) : null;
    }
}