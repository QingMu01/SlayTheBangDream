using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.GameHook;
using BangDreamInSpire.Scripts.Mechanics.Perform.Chord;
using BangDreamInSpire.Scripts.Mechanics.Perform.Site;
using BangDreamInSpire.Scripts.Relics;
using ItsCrychic.Scripts.Character;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models.Capabilities;

namespace ItsCrychic.Scripts.Relics.GameRules;

/// <summary>
/// 睦的演奏触发规则：打出带和弦的卡牌后，激活该和弦对应分组中音乐牌的奏响。
/// 和弦由卡牌奖励随机赋予（<see cref="ChordCapability" />，随卡牌实例存档），
/// 只负责激活奏响，不影响卡牌入组（入组由 <c>PerformTargetTypes.RequestGroup</c>
/// 或方案默认分组决定）。
/// </summary>
[RegisterRelic(typeof(DeprecatedRelicPool))]
public class MutsumiSpecialRules : HiddenRelic, IPerformTriggerListener
{
    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        var card = cardPlay.Card;
        var chord = card.TryGetCapability<ChordCapability>(out var chordCapability)
            ? chordCapability.Chord
            : PerformChord.None;
        if (chord == PerformChord.None) return Task.CompletedTask;

        card.Owner.AttachedData().PerformManager.RequestChordPerform(chord);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 睦的休息处多出「和弦设计」：打开和弦面板，给卡组里的一张牌指定和弦。
    /// 只给睦，其他角色不注入（注入后返回 true 表示本规则处理了选项修改）。
    /// </summary>
    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        if (player.Character is WakabaMutsumi)
        {
            options.Add(new ChordDesignOption(player));
            return true;
        }

        return false;
    }
}
