using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class Kanon() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CardVars => [];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var hand = PileType.Hand.GetPile(Owner);
        var extraDraw = BangDreamConst.PileExtraDraw.GetPile(Owner);
        var drawCount = 0;
        while (hand.Cards.Count < CardPile.MaxCardsInHand && extraDraw.Cards.Count > 0)
        {
            var drawResult = await ExtraPileCmd.Draw(choiceContext, 1, Owner);
            if (drawResult.Any())
            {
                drawCount++;
            }
        }

        if (IsUpgraded && drawCount > 0)
        {
            await MusicNoteCmd.FromCard(choiceContext, this, drawCount);
        }
    }
}