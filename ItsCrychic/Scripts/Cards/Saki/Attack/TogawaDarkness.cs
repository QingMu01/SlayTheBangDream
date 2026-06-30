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

public class TogawaDarkness() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        ModCardVars.Int("GoldReq", 15),
        ComputedDynamicVarHelper.CreateDamageVar("CalcDamage", 9m, CalculateDamage)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        await DamageCmd.Attack(DynamicVars.ComputedValue("CalcDamage"))
            .FromCard(this)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["GoldReq"].UpgradeValueBy(-5);
    }

    private static decimal CalculateDamage(CardModel? card, Creature? target)
    {
        if (card is { IsMutable: true })
        {
            return 9m + Math.Round(card.Owner.Gold / card.DynamicVars["GoldReq"].BaseValue);
        }

        return 9m;
    }
}