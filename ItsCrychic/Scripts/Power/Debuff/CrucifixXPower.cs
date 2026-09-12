using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Power.Debuff;

/// <summary>
/// 受到的卡牌攻击伤害与音符伤害均按百分比提高，向下取整由伤害管线统一处理。
/// </summary>
public class CrucifixXPower : BandPowerModel, IMusicNoteModifyHookListener
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        return target == Owner ? 1m + Amount / 100m : 1m;
    }

    public decimal ModifyMusicNoteDamageMultiplicative(Creature? target, decimal amount,
        Creature? dealer, AbstractModel? source)
    {
        return target == Owner ? 1m + Amount / 100m : 1m;
    }
}
