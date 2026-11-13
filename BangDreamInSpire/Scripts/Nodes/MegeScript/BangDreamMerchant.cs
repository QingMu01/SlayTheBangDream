using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Utils;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using STS2RitsuLib.Scaffolding.Characters.Visuals;

namespace BangDreamInSpire.Scripts.Nodes.MegeScript;

public partial class BangDreamMerchant : NMerchantCharacter
{
    private string? _animName;

    public override void _Ready()
    {
        // 本体的 NMerchantCharacter._Ready 与 PlayAnimation 都会直接 new MegaSprite(GetChild(0))，
        // 第一子节点不是 SpineSprite 时（静态纹理商人场景）会抛异常，且 MegaSprite 的构造在校验之前，
        // 没有任何可以兜底的 try 点。因此这里先判形态：非 Spine 时改走 RitsuLib 的播放层。
        if (!HasSpineVisual())
        {
            if (_animName != null)
            {
                ModCreatureVisualPlayback.TryPlayOnVisualRoot(this, null, _animName, loop: true);
            }

            return;
        }

        if (_animName != null)
        {
            PlayAnimation(_animName, true);
        }
        else
        {
            base._Ready();
        }
    }

    private bool HasSpineVisual() =>
        GetChildCount() > 0 && GetChild(0).GetClass() == MegaSprite.spineClassName;

    public static NMerchantCharacter? Create(Player player)
    {
        if (player.Character is ISkinSupportCharacter)
        {
            var skinInfo = BangDreamConst.PlayerSkin.Get(player).GetSkin()?.SkinTemplate;
            var path = skinInfo?.MultiplayerVisual.MerchantScene;
            var name = skinInfo?.MultiplayerVisual.MerchantAnimName;
            if (path != null)
            {
                var merchant = BangDreamPreloadManager.GetScene(path).Instantiate<BangDreamMerchant>();
                if (!string.IsNullOrEmpty(name))
                {
                    merchant._animName = name;
                    BangDreamInSpireCore.Logger.Info($"Use Skin Support Merchant Replace Merchant Scene({path})");
                    return merchant;
                }
            }
        }

        return null;
    }
}
