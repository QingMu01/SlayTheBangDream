using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class TheWholeBlueWorldSakiko() : AbstractSakikoMusicCard(CustomRarity, CustomTarget)
{
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private static readonly TargetType CustomTarget = PerformTargetTypes.RequestGroup;

    public override bool IsInstant => true;

    protected override IEnumerable<DynamicVar> CardVars => [];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        var performCards = BangDreamConst.PerformPile.GetPile(Owner).Cards
            .Where(card => card != this)
            .ToList();
        if (performCards.Count == 0) return;

        // 先记录丢弃数量，再丢弃并补给等量的能量与抽牌。
        var count = performCards.Count;
        await CardCmd.Discard(choiceContext, performCards);

        if (IsUpgraded)
        {
            await PlayerCmd.GainEnergy(count, Owner);
        }

        await CardPileCmd.Draw(choiceContext, count, Owner);
    }
}
