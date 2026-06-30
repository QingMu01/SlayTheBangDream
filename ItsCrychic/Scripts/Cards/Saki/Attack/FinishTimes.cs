using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class FinishTimes() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 2;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.PerformanceArea
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        ComputedDynamicVarHelper.CreateDamageVar("BaseDamage", 4m, CalculateDamage),
        QuickVar.Repeat.Create(4)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        await DamageCmd.Attack(DynamicVars.ComputedValue("BaseDamage"))
            .FromCard(this)
            .WithHitCount(DynamicVars.Repeat.IntValue)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    private static decimal CalculateDamage(CardModel? cardModel, Creature? target)
    {
        if (cardModel != null)
        {
            var performanceCards = BangDreamTools.GetPile(BangDreamConst.PerformanceTable, cardModel.Owner).Cards.ToList();
            var card = performanceCards.FirstOrDefault();
            if (card != null)
            {
                if (performanceCards.All(c => c.Type == card.Type))
                {
                    var multiplier = cardModel.IsUpgraded ? 2m : 1.5m;
                    return 4m * multiplier;
                }
            }
        }

        return 4m;
    }
}