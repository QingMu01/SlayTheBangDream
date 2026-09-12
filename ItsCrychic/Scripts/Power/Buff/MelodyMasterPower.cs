using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Powers;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ItsCrychic.Scripts.Power.Buff;

public class MelodyMasterPower : BandPowerModel, IPerformHookListener
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task OnCardPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        if (perform.Card.Owner != Owner.Player) return;

        Flash();
        await CardPileCmd.Draw(choiceContext, Amount, Owner.Player);
    }
}