using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Power.Buff;

public class BlackeningPower : BandPowerModel
{
    private const int HpLossAmount = 3;
    private const int EchoFillTarget = 6;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card is IPerformanceCard && cardPlay.Card.Owner == Owner.Player)
        {
            Flash();

            cardPlay.Card.AddKeyword(CardKeyword.Exhaust);

            await CreatureCmd.Damage(choiceContext, Owner,
                new DamageVar(HpLossAmount, ValueProp.Unpowered | ValueProp.Unblockable), Owner);

            var currentLe = Owner.Player?.AttachedData().LingeredEnergy.Counter ?? 0;
            if (currentLe < EchoFillTarget)
            {
                await LingeredCmd.JustAdd(Owner.Player!, EchoFillTarget - currentLe);
            }
        }
    }
}