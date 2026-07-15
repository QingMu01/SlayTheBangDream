using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace BangDreamLib.Scripts.Interfaces.GameHook;

public interface ISubsideHookListener
{
    Task AfterCardSubside(PlayerChoiceContext choiceContext, CardPlay play);
}