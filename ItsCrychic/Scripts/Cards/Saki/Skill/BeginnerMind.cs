using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class BeginnerMind() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.Self;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.Music
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Cards.Create(2),
        QuickVar.Energy.Create(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var drawnCards = await ExtraPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);

        var musicCount = drawnCards.Count(card => card is IPerformCard);
        if (musicCount > 0)
        {
            await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue * musicCount, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
