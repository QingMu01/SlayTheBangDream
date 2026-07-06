using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class AveMujica() : AbstractSakikoMusicCard(CustomRarity, CustomTarget)
{
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.None;

    private readonly List<CardModel> _effectCards = [];

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.Perform
    ];

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.ReplayStatic)
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Repeat.Create(1)
    ];

    public override Task OnStartPerform(PlayerChoiceContext choiceContext)
    {
        var combatState = Owner.PlayerCombatState!;

        foreach (var card in combatState.AllPiles.SelectMany(pile => pile.Cards))
        {
            if (card.Rarity == CardRarity.Basic && card is not IPerformCard)
            {
                card.BaseReplayCount += DynamicVars.Repeat.IntValue;
                _effectCards.Add(card);
            }
        }

        return Task.CompletedTask;
    }

    public override Task OnStopPerform(PlayerChoiceContext choiceContext)
    {
        if (!IsUpgraded)
        {
            foreach (var effectCard in _effectCards)
            {
                effectCard.BaseReplayCount -= DynamicVars.Repeat.IntValue;
                if (effectCard.BaseReplayCount <= 0)
                {
                    effectCard.BaseReplayCount = 0;
                }
            }

            _effectCards.Clear();
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (_effectCards.Contains(play.Card))
        {
            if (Handle != null && play.PlayIndex == 0)
            {
                FlashInArea();
            }

            if (IsUpgraded)
            {
                play.Card.BaseReplayCount -= DynamicVars.Repeat.IntValue;
                if (play.Card.BaseReplayCount <= 0)
                {
                    play.Card.BaseReplayCount = 0;
                }

                _effectCards.Remove(play.Card);
            }
        }

        return Task.CompletedTask;
    }
}