using BangDreamLib.Scripts.Interfaces.CardAugment;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Features.Rules;

public class CopySelfAndPlayCardRule : SingletonModel
{
    public override bool ShouldReceiveCombatHooks => true;

    public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var shouldCopySelfAndPlay = false;
        if (play.Card is ICopySelfAndPlayFlag flag)
        {
            if (flag.ShouldCopySelfAndPlayOnce)
            {
                shouldCopySelfAndPlay = true;
                flag.ShouldCopySelfAndPlayOnce = false;
            }
            else if (flag.ShouldCopySelfAndPlay)
            {
                shouldCopySelfAndPlay = true;
            }
        }

        if (shouldCopySelfAndPlay)
        {
            var cardModel = play.Card.CreateDupe();
            if (play.Target == null)
            {
                await CardCmd.AutoPlay(choiceContext, cardModel, null);
            }
            else
            {
                await CardCmd.AutoPlay(choiceContext, cardModel, play.Target.IsHittable ? play.Target : null);
            }
        }
    }
}