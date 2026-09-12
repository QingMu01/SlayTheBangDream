using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class FinishTimes() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 2;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    protected override IEnumerable<CardKeyword> CardKeywords => [BangDreamConst.PerformArea];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(7),
        QuickVar.Repeat.Create(3)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        var performPile = BangDreamConst.PerformPile.GetPile(Owner);
        var hasBonus = performPile.Cards.Count >= 3;
        if (hasBonus)
        {
            var discarded = Owner.RunState.Rng.CombatCardSelection.NextItem(performPile.Cards);
            if (discarded is IPerformCard performCard)
            {
                var location = performCard.StopPerformanceNextPile();
                await CardPileCmd.Add(discarded, location.pileType, location.position);
            }
            else if (discarded != null)
            {
                await CardPileCmd.Add(discarded, PileType.Discard);
            }
        }

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue * (hasBonus ? 2 : 1))
            .FromCard(this, play)
            .WithHitCount(DynamicVars.Repeat.IntValue)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(1);
    }
}
