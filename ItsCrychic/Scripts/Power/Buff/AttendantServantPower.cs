using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Powers;
using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Cards.Token;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.CardPiles;

namespace ItsCrychic.Scripts.Power.Buff;

public class AttendantServantPower : BandPowerModel, IMusicNotePlayedHook
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner.Player)
        {
            await PowerCmd.Remove(this);
        }
    }

    public async Task OnMusicNotePlayed(PlayerChoiceContext choiceContext)
    {
        ArgumentNullException.ThrowIfNull(Owner.CombatState);

        if (Owner.Player != null)
        {
            Flash();
            await CardPileCmd.AddGeneratedCardToCombat(Owner.CombatState.CreateCard<SakikoShield>(Owner.Player),
                BangDreamConst.PileExtraDraw.GetModCardPileType(),
                Owner.Player, CardPilePosition.Top);
        }
    }
}