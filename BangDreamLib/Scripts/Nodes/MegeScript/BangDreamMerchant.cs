using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace BangDreamLib.Scripts.Nodes.MegeScript;

public partial class BangDreamMerchant : NMerchantCharacter
{
    private string? _animName;

    public override void _Ready()
    {
        if (_animName != null)
        {
            PlayAnimation(_animName);
        }
        else
        {
            base._Ready();
        }
    }

    public static NMerchantCharacter? Create(NMerchantCharacter? original, Player player)
    {
        if (player.Character is ISkinSupportCharacter)
        {
            var skinInfo = BangDreamConst.PlayerSkin.Get(player).GetSkin()?.SkinTemplate;
            var path = skinInfo?.MultiplayerVisual.MerchantScene;
            var name = skinInfo?.MultiplayerVisual.MerchantAnimName;
            if (path != null)
            {
                var merchant = PreloadManager.Cache.GetScene(path).Instantiate<BangDreamMerchant>();
                if (!string.IsNullOrEmpty(name))
                {
                    merchant._animName = name;
                    BangDreamLibCore.Logger.Info($"Use Skin Support Merchant Replace Merchant Scene({path})");
                    return merchant;
                }
            }
        }

        return original;
    }
}