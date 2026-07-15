using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Interfaces.GameHook;

public interface IMusicNoteModifyHookListener
{
    /// <summary>
    /// 修改音符基础伤害值
    /// </summary>
    decimal ModifyMusicNoteDamageAdditive(Creature? target, decimal amount,
        Creature? dealer, AbstractModel? source)
    {
        return 0m;
    }

    /// <summary>
    /// 修改音符最终伤害倍率
    /// </summary>
    decimal ModifyMusicNoteDamageMultiplicative(Creature? target, decimal amount,
        Creature? dealer, AbstractModel? source)
    {
        return 1m;
    }

    /// <summary>
    /// 修改音符发射次数
    /// </summary>
    decimal ModifyMusicNoteShotCount(decimal amount, Creature? dealer, AbstractModel? source)
    {
        return amount;
    }

    /// <summary>
    /// 音符弹跳次数
    /// </summary>
    decimal ModifyMusicNoteBounceCount(decimal amount, Creature? dealer, AbstractModel? source)
    {
        return amount;
    }
}