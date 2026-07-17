using BangDreamLib.Scripts.Interfaces.CardAugment;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

namespace BangDreamLib.Scripts.Features.Rule;

[RegisterSingleton]
public class CopySelfAndPlayCardRule() : HookedSingletonModel(HookType.Combat)
{
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
            var cardModel = play.Card.CreateDupe(play.Player);
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