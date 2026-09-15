using BangDreamLib.Scripts.Enums;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Mechanics.Perform.Chord;
using BangDreamLib.Scripts.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ItsCrychic.Scripts.Relics.GameRules;

public class MutsumiSpecialRules : HiddenRelic, IPerformTriggerListener
{
    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        var card = cardPlay.Card;
        var chord = PerformChordStore.GetChord(card);
        if (chord == PerformChord.None) return Task.CompletedTask;

        card.Owner.AttachedData().PerformManager.RequestChordPerform(chord);
        return Task.CompletedTask;
    }
}