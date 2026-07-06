using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class Ether() : AbstractSakikoMusicCard(CustomRarity, CustomTarget)
{
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust,
        BangDreamConst.Perform,
        BangDreamConst.PerformArea,
        BangDreamConst.Instant
    ];

    protected override IEnumerable<DynamicVar> CardVars => [];


    public override async Task OnStartPerform(PlayerChoiceContext choiceContext)
    {
        var cardPile = BangDreamConst.PerformPile.GetPile(Owner);
        foreach (var card in cardPile.Cards)
        {
            if (card.Tags.Contains(BangDreamConst.SymbolCard) && card is IPerformCard performanceCard)
            {
                await performanceCard.OnStopPerform(choiceContext);
            }
        }
    }

    public override Task OnStopPerform(PlayerChoiceContext choiceContext)
    {
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}