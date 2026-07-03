using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.GameHook;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ItsCrychic.Scripts.Relics.Sakiko;

public class RolandKeyboard : AbstractSakikoRelic, IModifyLingeredHook
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    private bool _canEffective = true;

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        _canEffective = true;
        return Task.CompletedTask;
    }

    public decimal ModifyLingeredEnergyReduce(decimal amount)
    {
        return _canEffective ? 0m : amount;
    }

    public override Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card is ISubsideCard { LingeredEnergyCost: > 0 })
        {
            Flash();
            _canEffective = false;
        }

        return Task.CompletedTask;
    }
}