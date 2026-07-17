using BangDreamLib.Scripts.Powers;
using ItsCrychic.Scripts.Cards.Token;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ItsCrychic.Scripts.Power.Debuff;

public class AmnesiaPower : BandPowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    // Todo: 非最终效果
    public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (LocalContext.IsMe(Owner) && cardPlay.Card.Owner.Creature == Owner && cardPlay.Card.Type == CardType.Attack)
        {
            if (Owner.Player?.PlayerCombatState != null)
            {
                var selectedHandCard =
                    Owner.Player.RunState.Rng.CombatCardSelection.NextItem(Owner.Player.PlayerCombatState.Hand.Cards);
                if (selectedHandCard != null)
                {
                    await CardCmd.TransformTo<SakikoShield>(selectedHandCard);
                }
            }
        }
    }
}