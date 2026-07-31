using BangDreamLib.Scripts.Extensions;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class WonderfulWorld() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None)
{
    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Cards.Create(1)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext)
    {
        ArgumentNullException.ThrowIfNull(Owner.PlayerCombatState);
        var cardModel = Owner.RunState.Rng.CombatCardSelection.NextItem(Owner.PlayerCombatState.Hand.Cards);
        if (cardModel != null)
        {
            var result = await CardCmd.TransformToRandom(cardModel, Owner.RunState.Rng.CombatCardGeneration);
            if (result.success)
            {
                if (IsUpgraded)
                {
                    result.cardAdded.AddKeyword(CardKeyword.Exhaust);
                }
            }
        }
    }
}