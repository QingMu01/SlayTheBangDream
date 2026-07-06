using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class FutatsuNoTsuki() : AbstractSakikoMusicCard(CardRarity.Rare, TargetType.None), IPerformAreaHook
{
    public override bool IsInstant { get; set; } = true;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust,
        BangDreamConst.Instant,
        BangDreamConst.Perform
    ];

    private bool _isEffected;

    protected override IEnumerable<DynamicVar> CardVars => [];

    public override Task OnStartPerform(PlayerChoiceContext choiceContext)
    {
        _isEffected = false;
        return Task.CompletedTask;
    }

    public override Task OnStopPerform(PlayerChoiceContext choiceContext)
    {
        _isEffected = false;
        return Task.CompletedTask;
    }

    public async Task OnCardEnterPerformArea(CardModel cardModel)
    {
        if (Handle != null && cardModel is IPerformCard && !_isEffected)
        {
            _isEffected = true;
            FlashInArea();
            var manager = Owner.AttachedData().PerformManager;

            foreach (var inAreaCard in manager.PerformancePile.Cards.ToList())
            {
                if (inAreaCard != cardModel)
                {
                    var dupe = cardModel.CreateDupe();
                    if (IsUpgraded)
                    {
                        CardCmd.Upgrade(dupe);
                    }

                    CardCmd.PreviewCardPileAdd(
                        await CardPileCmd.AddGeneratedCardToCombat(dupe, BangDreamConst.PerformPile, Owner));
                }
            }
        }
    }
}