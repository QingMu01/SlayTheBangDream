using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class Finale() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 3;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.AllEnemies;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new IntVar("IncreaseStep", 2),
        ModCardVars.Computed("CalcDamage", 8m, card =>
                DynamicVarHelper.ResolveBaseVar(card, CalculateDamage),
            (card, mode, target, runHooks) =>
                DynamicVarHelper.ResolvePreviewDamageVar(card, mode, target, runHooks, CalculateDamage)),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        await DamageCmd.Attack(this.DynamicVar<ComputedDynamicVar>("CalcDamage").Calculate())
            .FromCard(this)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    private static decimal CalculateDamage(CardModel? card)
    {
        if (card != null)
        {
            var intValue = card.DynamicVar<IntVar>("IncreaseStep").BaseValue;
            var musicNoteCount = card.Owner.AttachedData().MusicNoteDamageTracker.GetAllDamageResults().Count;
            return 8m + intValue * musicNoteCount;
        }

        return 8m;
    }
}