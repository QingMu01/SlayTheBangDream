using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Power.Buff;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class RewindImpact()
    : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), ISubsideCard
{
    private const int CustomCost = 2;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Common;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    public int LingeredResourceCost => 4;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.Lingered,
        BangDreamConst.PerformArea
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(14)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, play)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        var cards = Owner.AttachedData().PerformManager.PerformPile.Cards.ToList();
        await CardPileCmd.Add(cards, BangDreamConst.ExtraDraw, CardPilePosition.Random);
    }

    public Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        return PowerCmd.Apply<NextTurnLingeredPower>(choiceContext, Owner.Creature, 18,
            Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(6m);
    }
}