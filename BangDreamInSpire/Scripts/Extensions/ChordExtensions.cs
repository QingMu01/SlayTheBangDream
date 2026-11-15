using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Mechanics.Perform.Chord;
using BangDreamInSpire.Scripts.Nodes.Screens;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Models.Capabilities;

namespace BangDreamInSpire.Scripts.Extensions;

/// <summary>
/// 卡牌和弦的稳定扩展入口。内容模组通过本类读写和弦，不直接依赖 <c>ChordCapability</c> 或休息处选项实现。
/// </summary>
public static class ChordExtensions
{
    /// <summary>读取卡牌当前和弦；未赋予时返回 <see cref="PerformChord.None" />。</summary>
    public static PerformChord GetChord(this CardModel card)
        => card.TryGetCapability<ChordCapability>(out var capability)
            ? capability.Chord
            : PerformChord.None;

    /// <summary>赋予卡牌和弦；传 <see cref="PerformChord.None" /> 表示移除。</summary>
    public static void SetChord(this CardModel card, PerformChord chord)
        => card.GetOrCreateCapability<ChordCapability>().SetChord(chord);

    /// <summary>创建「和弦设计」休息处选项。</summary>
    public static RestSiteOption CreateChordDesignOption(this Player player)
        => new ChordDesignOption(player);
}
