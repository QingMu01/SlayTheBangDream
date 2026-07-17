using BangDreamLib.Scripts.Extensions;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class EleganceGreeting() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Common;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    protected override HashSet<CardKeyword> CardKeywords =>
    [
        CardKeyword.Innate,
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<IHoverTip> CardHoverTips
    {
        get
        {
            yield return HoverTipFactory.FromPower<WeakPower>();
            if (IsUpgraded)
            {
                yield return HoverTipFactory.FromPower<VulnerablePower>();
            }
        }
    }

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(7),
        QuickVar.Buff.Create(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        var attackCommand = await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, play)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        var damageResult = attackCommand.Results.SelectMany(r => r).FirstOrDefault();
        if (damageResult is { Receiver.IsHittable: true })
        {
            var debuff = QuickVar.Buff.GetVar(this).IntValue;
            await PowerCmd.Apply<WeakPower>(choiceContext, damageResult.Receiver, debuff,
                Owner.Creature, this);
            if (IsUpgraded)
            {
                await PowerCmd.Apply<WeakPower>(choiceContext, damageResult.Receiver, debuff,
                    Owner.Creature, this);
            }
        }
    }
}