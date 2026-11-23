using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.GameHook;
using BangDreamInSpire.Scripts.Relics;
using ItsCrychic.Scripts.Character;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2RitsuLib.Interop.AutoRegistration;

namespace ItsCrychic.Scripts.Relics.GameRules;

/// <summary>打出带和弦的卡牌时奏响对应分组，不改变入组规则。</summary>
[RegisterRelic(typeof(DeprecatedRelicPool))]
public class MutsumiSpecialRules : HiddenRelic, IPerformTriggerListener
{
    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        var card = cardPlay.Card;
        var chord = card.GetChord();
        if (chord == PerformChord.None) return Task.CompletedTask;

        card.Owner.AttachedData().Perform.RequestChordPerform(chord);
        return Task.CompletedTask;
    }

    /// <summary>为睦注入休息处「和弦设计」选项。</summary>
    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        if (player.Character is WakabaMutsumi)
        {
            options.Add(player.CreateChordDesignOption());
            return true;
        }

        return false;
    }
}
