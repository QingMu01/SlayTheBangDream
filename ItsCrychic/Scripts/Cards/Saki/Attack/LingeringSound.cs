using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class LingeringSound()
    : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), ISubsideCard
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    public int LingeredResourceCost => -1;

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromPower<VigorPower>()
    ];

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.Lingered
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(1),
        ComputedDynamicVarHelper.CreateBaseVar("RepeatCount", 0, ctx => ctx.IsInCombat()
            ? SecondaryResourceCmd.Get(ctx.ActiveCard.Owner, BangDreamConst.LingeredResource)
            : ctx.BaseValue)
    ];
    
    //TODO Bug检查
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);
        ArgumentNullException.ThrowIfNull(play.Target);

        var computedValue = DynamicVars.ComputedValue("RepeatCount");
        if (computedValue > 0)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, play)
                .Targeting(play.Target)
                .WithHitCount((int)computedValue)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }
    }

    public async Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var value = play.SecondaryResources().Value(BangDreamConst.LingeredResource);
        if (IsUpgraded)
        {
            value += 3;
        }

        if (value > 0)
        {
            await PowerCmd.Apply<VigorPower>(choiceContext, Owner.Creature, value, Owner.Creature, this);
        }
    }
}