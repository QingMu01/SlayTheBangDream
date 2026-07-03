using BangDreamLib.Scripts.Powers;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Cards.DynamicVars;

namespace ItsCrychic.Scripts.Power.Buff;

public class NextTurnPerformPower : BandPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    private CardModel? _holdCard;

    protected override IEnumerable<DynamicVar> PowerVars =>
    [
        ModCardVars.String("CardName", _holdCard?.Title ?? "")
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner.Player && _holdCard != null)
        {
            await CardPileCmd.Add(_holdCard, BangDreamConst.PerformPile);
            await PowerCmd.Remove(this);
        }
    }

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (_holdCard == null)
        {
            await PowerCmd.Remove(this);
        }
    }

    public void SetHoldCard(CardModel card)
    {
        _holdCard = card;
    }
}