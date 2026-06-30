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

public class KamisamaBaka() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None)
{
    protected override IEnumerable<CardKeyword> CardKeywords => [BangDreamConst.Performance];

    protected override IEnumerable<DynamicVar> CardVars => [QuickVar.Damage.Create(10)];

    public override async Task AfterBlockGained(Creature target, decimal amount, ValueProp props, CardModel? cardSource)
    {
        if (Handle == null || CombatState == null || !CombatState.HittableEnemies.Contains(target) || target.Block <= 0)
        {
            return;
        }

        await CreatureCmd.LoseBlock(target, target.Block);
        await CreatureCmd.Damage(new BlockingPlayerChoiceContext(), target,
            new DamageVar(DynamicVars.Damage.BaseValue, ValueProp.Unpowered), this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(5);
    }
}