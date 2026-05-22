using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using STS2RitsuLib.Scaffolding.Godot;

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

    public static NMerchantCharacter? Create(Player player)
    {
        if (player.Character is ISkinSupportCharacter)
        {
            var skinInfo = player.AttachedData().SkinManager.CurrentSkin?.SkinTemplate;
            var path = skinInfo?.MultiplayerVisual.MerchantScene;
            var name = skinInfo?.MultiplayerVisual.MerchantAnimName;
            BangDreamMerchant? merchant = null;
            if (path != null)
            {
                merchant = RitsuGodotNodeFactories.CreateFromScenePath<BangDreamMerchant>(path);
                if (!string.IsNullOrEmpty(name))
                {
                    merchant._animName = name;
                }
            }

            return merchant;
        }

        return PreloadManager.Cache.GetScene(player.Character.MerchantAnimPath).Instantiate<NMerchantCharacter>();
    }
}