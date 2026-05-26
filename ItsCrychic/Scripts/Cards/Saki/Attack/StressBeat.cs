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
using STS2RitsuLib.Keywords;

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
        BangDreamConst.KeywordLinger.GetModCardKeyword()
    ];

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromPower<VulnerablePower>()
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        ModCardVars.Int("VulnerableDamage", 3),
        ModCardVars.Computed("CalcDamage", 14m,
            (card, target) =>
                DynamicVarHelper.ResolveBaseVar(card, target, CalculateDamage),
            (card, mode, target, runHooks) =>
                DynamicVarHelper.ResolvePreviewDamageVar(card, mode, target, runHooks, CalculateDamage)),
        ModCardVars.Computed(nameof(VulnerablePower), 0m, card =>
                DynamicVarHelper.ResolveBaseVar(card, GetLingered),
            (card, _, _, _) => GetLingered(card))
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        await DamageCmd.Attack(DynamicVars.ComputeVar("CalcDamage").Calculate(play.Target))
            .FromCard(this)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        if (Owner.AttachedData().LingeredEnergy.Counter > 0)
        {
            await PowerCmd.Apply<VulnerablePower>(choiceContext, play.Target,
                Owner.AttachedData().LingeredEnergy.Counter,
                Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["VulnerableDamage"].UpgradeValueBy(2m);
    }

    private static decimal CalculateDamage(CardModel? cardModel, Creature? target)
    {
        var powerAmount = target?.GetPowerAmount<VulnerablePower>();
        if (cardModel != null && powerAmount.HasValue)
        {
            return 14m + powerAmount.Value * cardModel.DynamicVars["VulnerableDamage"].BaseValue;
        }

        return 14m;
    }

    private static decimal GetLingered(CardModel? cardModel)
    {
        var counter = cardModel?.Owner.AttachedData().LingeredEnergy.Counter;
        if (counter.HasValue)
        {
            return counter.Value;
        }

        return 0m;
    }
}