using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Entities.Players;

namespace BangDreamLib.Scripts.Interfaces.GameHook;

public interface IMusicNotePlayedHook
{
    Task OnMusicNoteSpawn(VfxContext context, Player player);
}