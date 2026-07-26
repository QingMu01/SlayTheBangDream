using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class KamisamaBaka() : AbstractSakikoMusicCard(CardRarity.Rare, TargetType.None)
{
    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Repeat.Create(1)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext)
    {
        var performCandidates = Owner.AttachedData().PerformManager.PerformPile.Cards
            .Where(card => card != this && !card.IsUpgraded && card is IPerformCard)
            .ToList();
        var performCard = Owner.RunState.Rng.CombatCardSelection.NextItem(performCandidates);
        if (performCard == null) return;

        CardCmd.Upgrade(performCard);
        for (var repeat = 0; repeat < DynamicVars.Repeat.IntValue; repeat++)
        {
            await Owner.AttachedData().PerformManager.PerformCard(performCard);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(1);
    }
}
