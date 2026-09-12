using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Power.Buff;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class StringEmpowerment() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 2;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    protected override IEnumerable<CardKeyword> CardKeywords => [BangDreamConst.Lingered];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(15),
        QuickVar.Energy.Create(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, play)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        await PowerCmd.Apply<LingeredReductionEnergyPower>(choiceContext, Owner.Creature,
            DynamicVars.Energy.IntValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
    }
}
