using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Interfaces.GameHook;

public interface IMusicNoteModifyHook
{
    decimal ModifyMusicNoteDamageAdditive(Creature? target, decimal amount,
        Creature? dealer, AbstractModel? source)
    {
        return amount;
    }

    decimal ModifyMusicNoteDamageMultiplicative(Creature? target, decimal amount,
        Creature? dealer, AbstractModel? source)
    {
        return amount;
    }

    decimal ModifyMusicNoteShotCount(decimal amount, Creature? dealer, AbstractModel? source)
    {
        return amount;
    }
}