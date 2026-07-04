using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Relics.Sakiko;

public class Synthesizer : AbstractSakikoRelic, ISecondaryResourceHookListener
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    private bool _canEffective = true;

    public override Task BeforeCombatStart()
    {
        _canEffective = true;
        return Task.CompletedTask;
    }

    public decimal ModifySecondaryResourceCost(SecondaryResourceCostContext context, decimal cost)
    {
        return _canEffective ? 0m : cost;
    }

    public override Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (!play.Card.IsDupe && _canEffective && play.Card.SecondaryCosts().HasCosts)
        {
            Flash();
            _canEffective = false;
        }

        return Task.CompletedTask;
    }
}