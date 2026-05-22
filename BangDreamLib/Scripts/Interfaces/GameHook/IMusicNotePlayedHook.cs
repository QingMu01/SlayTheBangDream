using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace BangDreamLib.Scripts.Interfaces.GameHook;

public interface IMusicNotePlayedHook
{
    Task OnMusicNotePlayed(PlayerChoiceContext choiceContext);
}