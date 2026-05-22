using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Power.Buff;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Keywords;

namespace ItsCrychic.Scripts.Cards.Saki.Power;

public class LessonLearned() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Power;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.Self;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.KeywordLinger.GetModCardKeyword()
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new IntVar("Resonance", 3),
        new CardsVar(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (IsUpgraded)
        {
            await ExtraPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
        }

        await PowerCmd.Apply<LessonLearnedPower>(choiceContext, Owner.Creature, DynamicVars["Resonance"].IntValue,
            Owner.Creature, this);
    }
}