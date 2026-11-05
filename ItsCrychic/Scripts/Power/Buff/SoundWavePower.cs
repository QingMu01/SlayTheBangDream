using BangDreamInSpire.Scripts.Mechanics.MusicNote;
using BangDreamInSpire.Scripts.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ItsCrychic.Scripts.Power.Buff;

public class SoundWavePower : BandPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;


    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        // hook 对全场每次出牌都会触发，只累计本能力拥有者的出牌。
        if (cardPlay.Player != Owner.Player)
            return;

        await PowerCmd.ModifyAmount(new BlockingPlayerChoiceContext(), this, 1M, null, null);
    }


    public override async Task AfterAutoPostPlayPhaseEntered(PlayerChoiceContext choiceContext, Player player)
    {
        if (Amount > 0 && player == Owner.Player)
        {
            await MusicNoteCmd.FromPower(this, Amount, shouldImmediate: true);
        }

        await PowerCmd.Remove(this);
    }
}