using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Entities.Players;

namespace BangDreamLib.Scripts.Interfaces.GameHook;

public interface IMusicNoteShotHookListener
{
    Task AfterShot(VfxContext context, Player player) => Task.CompletedTask;
}