using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Powers;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Power.Buff;

public class BlackeningPower : BandPowerModel
{
    private const int HpLossAmount = 3;
    private const int EchoFillTarget = 6;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card is IPerformanceCard && play.Card.Owner == Owner.Player)
        {
            Flash();

            play.Card.AddKeyword(CardKeyword.Exhaust);

            await CreatureCmd.Damage(choiceContext, Owner,
                new DamageVar(HpLossAmount, ValueProp.Unpowered | ValueProp.Unblockable), Owner);

            var currentLingeredResource = SecondaryResourceCmd.Get(Owner.Player, BangDreamConst.LingeredResource);
            if (currentLingeredResource < EchoFillTarget)
            {
                await SecondaryResourceCmd.Gain(Owner.Player, BangDreamConst.LingeredResource,
                    EchoFillTarget - currentLingeredResource, this);
            }
        }
    }
}