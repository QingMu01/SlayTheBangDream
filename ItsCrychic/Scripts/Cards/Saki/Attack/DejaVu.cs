using BangDreamLib.Scripts.Interfaces.CardAugment;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class DejaVu() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget),
    ISubsideCardFlag
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Common;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    public int LingeredEnergyCost => 3;
    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new DamageVar(3, ValueProp.Move),
        new RepeatVar(1)
    ];

    public Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        EnergyCost.AddThisCombat(-1, true);
        return Task.CompletedTask;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .WithHitCount(DynamicVars.Repeat.IntValue)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        DynamicVars.Repeat.BaseValue += 1;
        EnergyCost.AddThisCombat(1);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m);
    }
}