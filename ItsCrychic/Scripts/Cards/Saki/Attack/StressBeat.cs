using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class StressBeat()
    : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 2;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Common;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.Lingered
    ];

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromPower<VulnerablePower>()
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        ModCardVars.Int("VulnerableDamage", 3),
        ComputedDynamicVarHelper.CreateDamageVar("CalcDamage", 14m, CalculateDamage),
        ComputedDynamicVarHelper.CreateBaseVar(nameof(VulnerablePower), 0m,
            card => card == null ? 0m : SecondaryResourceCmd.Get(card.Owner, BangDreamConst.LingeredResource))
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        await DamageCmd.Attack(DynamicVars.ComputedValue("CalcDamage", play.Target))
            .FromCard(this, play)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        var lingeredResource = SecondaryResourceCmd.Get(Owner, BangDreamConst.LingeredResource);
        if (lingeredResource > 0)
        {
            await PowerCmd.Apply<VulnerablePower>(choiceContext, play.Target, lingeredResource, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["VulnerableDamage"].UpgradeValueBy(2m);
    }

    private static decimal CalculateDamage(CardModel? card, Creature? target)
    {
        var powerAmount = target?.GetPowerAmount<VulnerablePower>();
        if (card != null && powerAmount.HasValue &&
            card.DynamicVars.TryGetValue("VulnerableDamage", out var vulnerableDamage))
        {
            return 14m + powerAmount.Value * vulnerableDamage.BaseValue;
        }

        return 14m;
    }
}