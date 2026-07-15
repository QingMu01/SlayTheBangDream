using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Entities.Players;

namespace BangDreamLib.Scripts.Interfaces.GameHook;

public interface IMusicNoteShotHookListener
{
    Task OnMusicNoteSpawn(VfxContext context, Player player);
}