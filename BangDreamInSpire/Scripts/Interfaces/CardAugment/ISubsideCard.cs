using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace BangDreamInSpire.Scripts.Interfaces.CardAugment;

public interface ISubsideCard
{
    bool ShouldGenerateResources => false;

    int LingeredResourceCost { get; }

    Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play);
}