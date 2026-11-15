using BangDreamInSpire.Scripts.Mechanics;
using BangDreamInSpire.Scripts.Nodes.Cards;
using BangDreamInSpire.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Patching.Core;

namespace BangDreamInSpire.Scripts.Nodes;

/// <summary>
/// 卡面覆盖层工厂的 UI 侧注册机制。机制代码只依赖 <see cref="CardOverlayRegistry" />，
/// 具体节点创建留在 <c>Nodes</c> 层。
/// </summary>
public sealed class OverlayFactoriesMechanic : IBangDreamMechanic
{
    public string Id => "overlay_factories";

    public int Order => 10;

    public void RegisterContent(BangDreamMechanicContext context)
    {
        CardOverlayRegistry.Register("chord_badge", new ChordBadgeFactory());
        CardOverlayRegistry.Register("head_tip", new HeadTipFactory());
    }

    public void RegisterPatches(ModPatcher patcher)
    {
    }

    private sealed class ChordBadgeFactory : ICardOverlayFactory
    {
        public Control? Create(CardModel card) => NChordCornerBadge.Create(card);
    }

    private sealed class HeadTipFactory : ICardOverlayFactory
    {
        public Control? Create(CardModel card) => BangDreamHeadTip.Create(card);
    }
}
