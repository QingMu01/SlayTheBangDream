using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class WonderfulWorld() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None)
{
    protected override IEnumerable<DynamicVar> CardVars => [];

    public override async Task OnPerform(PlayerChoiceContext choiceContext)
    {
        ArgumentNullException.ThrowIfNull(Owner.PlayerCombatState);
        foreach (var card in Owner.PlayerCombatState.Hand.Cards.ToList())
        {
            var result = await CardCmd.TransformToRandom(card, Owner.RunState.Rng.CombatCardGeneration);
            if (result is { success: false }) continue;

            var replacement = result.cardAdded;
            if (IsUpgraded)
            {
                replacement.AddKeyword(CardKeyword.Exhaust);
            }

            await Cmd.CustomScaledWait(0.15f, 0.3f);
            await CardPileCmd.Add(replacement, PileType.Hand);
        }
    }
}