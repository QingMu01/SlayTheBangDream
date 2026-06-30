using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Cards.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class Legato() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Common;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.Linger
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        ComputedDynamicVarHelper.CreateDamageVar("BaseDamage", 6, CalculateDamage),
        ModCardVars.Int("PerLingerDamage", 1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        await DamageCmd.Attack(DynamicVars.ComputedValue("BaseDamage"))
            .FromCard(this)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["PerLingerDamage"].UpgradeValueBy(1);
    }

    private static decimal CalculateDamage(CardModel? cardModel, Creature? target)
    {
        if (cardModel == null)
        {
            return 6m;
        }

        var lingerCount = cardModel.Owner.AttachedData().LingeredEnergy.Counter;
        var totalDamage = lingerCount * cardModel.DynamicVars["PerLingerDamage"].IntValue;
        return 6m + totalDamage;
    }
}