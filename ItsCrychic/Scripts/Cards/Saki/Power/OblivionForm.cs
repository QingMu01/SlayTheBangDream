using ItsCrychic.Scripts.Power.Buff;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Power;

public class OblivionForm() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 3;
    private const CardType CustomType = CardType.Power;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.Self;

    protected override IEnumerable<CardKeyword> CardKeywords => [CardKeyword.Ethereal];

    protected override IEnumerable<DynamicVar> CardVars => [ModCardVars.Int("Multiplier", 3)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PowerCmd.Apply<OblivionFormPower>(choiceContext, Owner.Creature,
            DynamicVars["Multiplier"].IntValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Multiplier"].UpgradeValueBy(1);
    }
}
