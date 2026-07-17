using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Power.Buff;

public class IncreaseLoudspeakerPower : BandPowerModel, IMusicNoteModifyHookListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public decimal ModifyMusicNoteDamageMultiplicative(Creature? target, decimal amount, Creature? dealer,
        AbstractModel? source)
    {
        return dealer == Owner ? Amount : 1m;
    }
}