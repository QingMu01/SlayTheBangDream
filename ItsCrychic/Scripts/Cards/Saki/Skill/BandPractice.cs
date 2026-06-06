using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class BandPractice() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 2;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Common;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.Music
    ];

    protected override IEnumerable<DynamicVar> CardVars => [];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        while (BangDreamTools.GetPile(BangDreamConst.ExtraDraw, Owner).Cards.Count > 0)
        {
            var drawnCardsTask = await ExtraPileCmd.Draw(choiceContext, 1, Owner);
            var drawnCards = drawnCardsTask.ToList();
            if (drawnCards.Count == 0)
                break;
            var drawnCard = drawnCards.First();
            if (drawnCard is IPerformanceCard)
            {
                break;
            }
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}