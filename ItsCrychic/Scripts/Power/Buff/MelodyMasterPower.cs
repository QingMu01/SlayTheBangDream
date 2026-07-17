using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Powers;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Power.Buff;

public class MelodyMasterPower : BandPowerModel, IPerformHookListener
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task OnCardPerform(PlayerChoiceContext choiceContext, PerformContext ctx, CardModel cardModel)
    {
        if (cardModel.Owner != Owner.Player) return;
        Flash();
        await MusicNoteCmd.FromPower(this, Amount);
    }
}
