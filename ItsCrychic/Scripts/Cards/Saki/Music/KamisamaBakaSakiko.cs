using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class KamisamaBakaSakiko() : AbstractSakikoMusicCard(CustomRarity, CustomTarget)
{
    private const CardRarity CustomRarity = CardRarity.Rare;
    private static readonly TargetType CustomTarget = PerformTargetTypes.RequestGroup;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Repeat.Create(1)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        // 排除本卡的所有实例：否则抽到第二张同名卡会直接空转。
        var performCandidates = Owner.AttachedData().Perform.PerformPile.Cards
            .Where(card => card != this && card is not KamisamaBakaSakiko && !card.IsUpgraded &&
                           card is IPerformCard)
            .ToList();
        var performCard = Owner.RunState.Rng.CombatCardSelection.NextItem(performCandidates);
        if (performCard == null) return;
        CardCmd.Upgrade(performCard);
        for (var repeat = 0; repeat < DynamicVars.Repeat.IntValue; repeat++)
        {
            await Owner.AttachedData().Perform.PerformCard(performCard);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(1);
    }
}