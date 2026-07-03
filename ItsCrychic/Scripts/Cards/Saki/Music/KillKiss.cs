using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class KillKiss() : AbstractSakikoMusicCard(CardRarity.Rare, TargetType.RandomEnemy)
{
    public override bool IsInstant { get; set; } = true;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.Perform,
        BangDreamConst.Instant
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new BoolVar("HandPlay", false),
        ComputedDynamicVarHelper.CreateBaseVar("BaseDamage", 15, CalculateFixedDamage)
    ];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ((BoolVar)DynamicVars["HandPlay"]).BoolVal = !play.IsAutoPlay;
        return Task.CompletedTask;
    }

    public override async Task OnStartPerformance(PlayerChoiceContext choiceContext)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var target = Owner.RunState.Rng.CombatTargets.NextItem(CombatState.HittableEnemies);
        if (target != null)
        {
            await CreatureCmd.Damage(choiceContext, target, DynamicVars.ComputedValue("BaseDamage"),
                ValueProp.Unpowered | ValueProp.Unblockable, Owner.Creature, this, null);
        }
    }

    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (card == this)
        {
            var pileType = card.Pile?.Type;
            if (pileType == PileType.Hand)
            {
                ((BoolVar)card.DynamicVars["HandPlay"]).BoolVal = true;
            }
            else if (pileType == PileType.Discard || pileType == PileType.Draw || pileType == PileType.Exhaust ||
                     pileType == BangDreamConst.ExtraDraw || pileType == BangDreamConst.PerformPile)
            {
                ((BoolVar)card.DynamicVars["HandPlay"]).BoolVal = false;
            }
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars["BaseDamage"].UpgradeValueBy(5);
    }

    private static decimal CalculateFixedDamage(CardModel? card, Creature? target)
    {
        if (card != null)
        {
            var baseValue = card.DynamicVars["BaseDamage"].BaseValue;
            return ((BoolVar)card.DynamicVars["HandPlay"]).BoolVal ? baseValue * 2m : baseValue;
        }

        return 15m;
    }
}