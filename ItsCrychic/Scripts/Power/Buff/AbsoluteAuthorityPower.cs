using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Power.Buff;

public class AbsoluteAuthorityPower : BandPowerModel
{
    private bool _willTriggerNextTurn;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> PowerVars => [QuickVar.Energy.Create(1)];

    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result,
        ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && dealer != Owner && result.UnblockedDamage > 0)
        {
            _willTriggerNextTurn = true;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner.Player && _willTriggerNextTurn)
        {
            Flash();
            _willTriggerNextTurn = false;
            await PlayerCmd.GainEnergy(Amount, Owner.Player);
            await CardPileCmd.Draw(choiceContext, Amount, Owner.Player);
        }
    }
}