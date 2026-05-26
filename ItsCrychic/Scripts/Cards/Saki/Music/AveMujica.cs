using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Keywords;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class AveMujica() : AbstractSakikoMusicCard(CustomRarity, CustomTarget)
{
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.None;

    private List<CardModel> _effectCards = [];

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.KeywordPerformance.GetModCardKeyword()
    ];

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.ReplayStatic)
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Repeat.Create(1)
    ];

    public override Task OnStartPerformance(PlayerChoiceContext choiceContext)
    {
        var combatState = Owner.PlayerCombatState!;

        foreach (var card in combatState.AllPiles.SelectMany(pile => pile.Cards))
        {
            if (card.Rarity == CardRarity.Basic)
            {
                card.BaseReplayCount += QuickVar.Repeat.Get(DynamicVars).IntValue;
                _effectCards.Add(card);
            }
        }

        return Task.CompletedTask;
    }

    public override Task OnStopPerformance(PlayerChoiceContext choiceContext)
    {
        if (!IsUpgraded)
        {
            foreach (var effectCard in _effectCards)
            {
                effectCard.BaseReplayCount -= QuickVar.Repeat.Get(DynamicVars).IntValue;
            }
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (IsUpgraded && _effectCards.Contains(cardPlay.Card) && Handle == null)
        {
            cardPlay.Card.BaseReplayCount -= QuickVar.Repeat.Get(DynamicVars).IntValue;
            if (cardPlay.Card.BaseReplayCount <= 0)
            {
                cardPlay.Card.BaseReplayCount = 0;
            }
        }

        return Task.CompletedTask;
    }
}