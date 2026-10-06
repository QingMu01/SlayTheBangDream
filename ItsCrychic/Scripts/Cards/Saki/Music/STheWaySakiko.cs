using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Cards.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class STheWaySakiko() : AbstractSakikoMusicCard(CustomRarity, CustomTarget)
{
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private static readonly TargetType CustomTarget = PerformTargetTypes.AnySlot;

    public override bool IsInstant => true;

    // 本场战斗内已累计的同名牌伤害加成，随卡牌实例持续到战斗结束
    private decimal _combatIncrease;

    protected override IEnumerable<CardKeyword> CardKeywords => [];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        ComputedDynamicVarHelper.CreateDamageVar("CalcDamage", 7m, ctx =>
        {
            if (ctx.IsInCombat() && ctx.ActiveCard is STheWaySakiko self)
            {
                return ctx.BaseValue + self._combatIncrease;
            }

            return ctx.BaseValue;
        }),
        ModCardVars.Int("Increase", 4)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        await DamageCmd.Attack(DynamicVars.ComputedValue("CalcDamage"))
            .FromCard(this, null)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        // 加成在伤害结算后累加，本次演奏不享受本次提升
        var increase = BangDreamConst.PerformPile.GetPile(Owner).Cards.Count *
            DynamicVars["Increase"].IntValue;
        foreach (var sameNameCard in EnumerateCombatCards().OfType<STheWaySakiko>().Distinct())
        {
            sameNameCard._combatIncrease += increase;
        }
    }

    private IEnumerable<CardModel> EnumerateCombatCards()
    {
        return Owner.PlayerCombatState!.AllCards
            .Concat(BangDreamConst.ExtraDraw.GetPile(Owner).Cards)
            .Concat(BangDreamConst.PerformPile.GetPile(Owner).Cards);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["CalcDamage"].UpgradeValueBy(3m);
        DynamicVars["Increase"].UpgradeValueBy(2);
    }
}
