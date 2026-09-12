using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Power.Buff;

public class PuppetTheaterPower : BandPowerModel, ISubsideHookListener
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task AfterCardSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card.Owner != Owner.Player || Owner.CombatState == null) return;

        Flash();
        var hittableCreatures = Owner.CombatState.Enemies.Where(enemy => enemy.IsHittable).ToList();
        foreach (var enemy in hittableCreatures)
        {
            var damage = new DamageVar(Amount, ValueProp.Unpowered | ValueProp.Unblockable | ValueProp.SkipHurtAnim);
            await CreatureCmd.Damage(choiceContext, enemy, damage, Owner);
        }
    }
}