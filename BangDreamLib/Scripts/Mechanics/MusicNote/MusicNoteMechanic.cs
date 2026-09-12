using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Patching.Core;

namespace BangDreamLib.Scripts.Mechanics.MusicNote;

/// <summary>
/// 音符机制：音符发射/弹跳与伤害、命中同拍结算、音符伤害追踪与 VFX 容器。
/// </summary>
public sealed class MusicNoteMechanic : IBangDreamMechanic
{
    public string Id => "music_note";

    public int Order => 400;

    public IEnumerable<AbstractModel> InstantiatePlayerState(Player player)
    {
        yield return ModelDb.Singleton<MusicNoteDamageTracker>().MutableClone();
    }

    public void RegisterContent(BangDreamMechanicContext context)
    {
        BangDreamConst.MusicNote = context.RegisterCardKeyword("MusicNote");

        MusicNoteCmd.InitializeNetwork();
    }

    public void RegisterPatches(ModPatcher patcher)
    {
        patcher.RegisterPatches<VfxManagerPatches>();
    }

    public IEnumerable<AbstractModel> GetCombatHookModels(Player player)
    {
        yield return player.AttachedData().MusicNoteDamageTracker;
    }
}
