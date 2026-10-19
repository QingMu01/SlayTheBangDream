using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Entities.Players;

namespace BangDreamInSpire.Scripts.Interfaces.GameHook;

public interface IMusicNoteShotHookListener
{
    Task AfterShot(VfxContext context, Player player) => Task.CompletedTask;
}