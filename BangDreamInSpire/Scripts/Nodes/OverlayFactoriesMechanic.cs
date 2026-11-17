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
        // 注册键必须与 ChordCapability.OverlayId 逐字一致：不一致时 Resolve 返回 null，
        // 机制侧会静默跳过覆盖层（不报错、不显示），属于最难排查的一类失配。
        // 当前 ChordCapability.OverlayId = "chord_bookmark"（和弦角标已改为右侧箭头书签，
        // 旧名 "chord_corner_badge" 与 NChordCornerBadge 已随该改动移除）。
        CardOverlayRegistry.Register("chord_bookmark", new ChordBookmarkFactory());
        CardOverlayRegistry.Register("head_tip", new HeadTipFactory());
    }

    public void RegisterPatches(ModPatcher patcher)
    {
    }

    private sealed class ChordBookmarkFactory : ICardOverlayFactory
    {
        public Control? Create(CardModel card) => NChordBookmark.Create(card);
    }

    private sealed class HeadTipFactory : ICardOverlayFactory
    {
        public Control? Create(CardModel card) => BangDreamHeadTip.Create(card);
    }
}
