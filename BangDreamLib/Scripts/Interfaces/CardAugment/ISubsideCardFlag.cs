using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Interfaces.CardAugment;

public interface ISubsideCardFlag
{
    int LingeredEnergyCost { get; }

    bool ShouldIncreaseLingeredEnergy => false;

    bool IgnoreSubsideCost => DefaultIgnoreCostCondition(this);

    bool CanSubside => DefaultSubsideCondition(this);

    Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play);

    private static readonly Func<ISubsideCardFlag, bool> DefaultSubsideCondition = subsideCard =>
    {
        if (subsideCard is CardModel { CombatState: not null } card)
        {
            var finalCost = BangDreamHook.ModifyLingeredEnergyReduce(card.CombatState, subsideCard.LingeredEnergyCost);
            return card.Owner.AttachedData().LingeredEnergy.Counter >= finalCost;
        }

        return subsideCard.IgnoreSubsideCost;
    };

    private static readonly Func<ISubsideCardFlag, bool> DefaultIgnoreCostCondition =
        subsideCard => subsideCard is CardModel { IsDupe: true };
}