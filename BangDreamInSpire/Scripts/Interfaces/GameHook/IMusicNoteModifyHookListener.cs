using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Interfaces.GameHook;

public interface IMusicNoteModifyHookListener
{
    /// <summary>
    /// 在一批音符提交时捕获一次性伤害加成。
    /// 返回值会固化到该批音符，不受后续能力状态变化影响。
    /// </summary>
    decimal CaptureMusicNoteDamageAdditive(Creature? dealer, AbstractModel? source)
    {
        return 0m;
    }

    decimal ModifyMusicNoteDamageAdditive(Creature? target, decimal amount,
        Creature? dealer, AbstractModel? source)
    {
        return 0m;
    }

    decimal ModifyMusicNoteDamageMultiplicative(Creature? target, decimal amount,
        Creature? dealer, AbstractModel? source)
    {
        return 1m;
    }

    decimal ModifyMusicNoteShotCount(decimal amount, Creature? dealer, AbstractModel? source)
    {
        return amount;
    }

    decimal ModifyMusicNoteBounceCount(decimal amount, Creature? dealer, AbstractModel? source)
    {
        return amount;
    }
}
