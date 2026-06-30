using BangDreamLib.Scripts.Attributes;
using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Features.Rules;

[BangDreamIgnore]
public class LingeredEnergyRule : SingletonModel
{
    public override bool ShouldReceiveCombatHooks => true;

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        var playedCard = cardPlay.Card;
        if (playedCard is ISubsideCard { CanSubside: true } subsideCard)
        {
            await subsideCard.OnSubside(context, cardPlay);
            await Cmd.CustomScaledWait(0.1f, 0.2f);
            if (!subsideCard.IgnoreSubsideCost && playedCard.DynamicVars.TryGetValue("Subside", out var subsideVar))
            {
                await LingeredCmd.ReduceLeByCard(playedCard, subsideVar.IntValue);
            }
            else
            {
                BangDreamLibCore.Logger.Warn($"Card {playedCard.Title} does not have subside var.");
            }
        }
    }
}