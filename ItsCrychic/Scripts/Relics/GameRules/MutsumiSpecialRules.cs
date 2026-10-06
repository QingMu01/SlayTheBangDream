using BangDreamLib.Scripts.Enums;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Mechanics.Perform.Chord;
using BangDreamLib.Scripts.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Models.Capabilities;

namespace ItsCrychic.Scripts.Relics.GameRules;

/// <summary>
/// 睦的演奏触发规则：打出带和弦的卡牌后，激活该和弦对应分组中音乐牌的奏响。
/// 和弦由卡牌奖励随机赋予（<see cref="ChordCapability" />，随卡牌实例存档），
/// 只负责激活奏响，不影响卡牌入组（入组由 <c>PerformTargetTypes.RequestGroup</c>
/// 或方案默认分组决定）。
/// </summary>
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
}
