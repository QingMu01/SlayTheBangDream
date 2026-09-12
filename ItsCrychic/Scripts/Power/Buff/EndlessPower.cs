using BangDreamLib.Scripts.Mechanics.MusicNote;
using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Power.Buff;

public class EndlessPower : BandPowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        if (Applier != null && command.Attacker == Applier && command.CardPlay?.Card is { Type: CardType.Attack } &&
            command.DamageProps.HasFlag(ValueProp.Move))
        {
            var isAttackOwner = command.Results.SelectMany(r => r)
                .Where(result => result.Receiver == Owner)
                .Any(r => r.TotalDamage > 0);
            if (isAttackOwner)
            {
                await MusicNoteCmd.CustomShot(Applier, Amount);
            }
        }
    }
}