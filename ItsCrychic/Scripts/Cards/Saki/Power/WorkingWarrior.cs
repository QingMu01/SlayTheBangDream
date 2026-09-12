using BangDreamLib.Scripts.Extensions;
using ItsCrychic.Scripts.Power.Buff;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Cards.Saki.Power;

public class WorkingWarrior() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Power;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.Self;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.HpLoss.Create(5),
        QuickVar.Gold.Create(30)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.Damage(choiceContext, Owner.Creature, DynamicVars.HpLoss.BaseValue,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, this, play);
        await PowerCmd.Apply<WorkingWarriorPower>(choiceContext, Owner.Creature, DynamicVars.Gold.IntValue,
            Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Gold.UpgradeValueBy(10);
    }
}
