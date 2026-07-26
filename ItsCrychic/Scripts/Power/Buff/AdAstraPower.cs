using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Powers;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Power.Buff;

public class AdAstraPower : BandPowerModel, IPerformHookListener, IMusicNoteModifyHookListener
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card.Owner != Owner.Player || play.Card.EnergyCost.GetResolved() != 0) return;

        await PowerCmd.Apply<NextShotNoteIncreasePower>(choiceContext, Owner, Amount, Owner, null);
    }

    public async Task OnCardPerform(PlayerChoiceContext choiceContext, PerformContext ctx, CardModel cardModel)
    {
        if (cardModel.Owner != Owner.Player) return;

        await PowerCmd.Apply<NextShotNoteIncreasePower>(choiceContext, Owner, Amount, Owner, null);
    }
}