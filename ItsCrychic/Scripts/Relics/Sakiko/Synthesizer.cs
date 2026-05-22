using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.GameHook;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ItsCrychic.Scripts.Relics.Sakiko;

public class Synthesizer : AbstractSakikoRelic, IModifyLingeredHook
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    private bool _canEffective = true;

    public override Task BeforeCombatStart()
    {
        _canEffective = true;
        return Task.CompletedTask;
    }

    public decimal ModifyLingeredEnergyReduce(decimal amount)
    {
        return _canEffective ? 0m : amount;
    }

    public override Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!cardPlay.Card.IsDupe && _canEffective && cardPlay.Card is ISubsideCardFlag { LingeredEnergyCost: > 0 })
        {
            Flash();
            _canEffective = false;
        }

        return Task.CompletedTask;
    }
}