using BangDreamLib.Scripts.Attributes;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Features.Rules;

[BangDreamIgnore]
public class CopySelfAndPlayCardRule : SingletonModel
{
    public override bool ShouldReceiveCombatHooks => true;

    public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var shouldCopySelfAndPlay = false;
        if (cardPlay.Card is ICopySelfAndPlayFlag flag)
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
            var cardModel = cardPlay.Card.CreateDupe();
            if (cardPlay.Target == null)
            {
                await CardCmd.AutoPlay(choiceContext, cardModel, null);
            }
            else
            {
                await CardCmd.AutoPlay(choiceContext, cardModel, cardPlay.Target.IsHittable ? cardPlay.Target : null);
            }
        }
    }
}