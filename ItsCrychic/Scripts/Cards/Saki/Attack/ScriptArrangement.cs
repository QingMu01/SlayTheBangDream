using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class ScriptArrangement() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.AllEnemies;

    protected override bool HasEnergyCostX => true;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new DamageVar(7m, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var hitCount = ResolveEnergyXValue();
        if (hitCount > 0)
        {
            var enemies = CombatState.HittableEnemies.ToList();
            var attackedEnemies = new HashSet<Creature>();
            for (var i = 0; i < hitCount; i++)
            {
                var enemy = GetNextTarget(enemies, i);
                if (enemy == null)
                {
                    return;
                }

                await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                    .FromCard(this)
                    .Targeting(enemy)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(choiceContext);
                if (attackedEnemies.Add(enemy))
                {
                    await ExtraPileCmd.Draw(choiceContext, 1, Owner);
                }
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }

    private Creature? GetNextTarget(List<Creature> enemies, int startIndex)
    {
        if (enemies.Count == 0)
            return null;
        if (enemies.All(e => !e.IsHittable))
            return null;
        if (enemies.Count == 1)
            return enemies[0];

        var searchCount = 0;
        var currentIndex = startIndex;
        while (searchCount < enemies.Count)
        {
            if (enemies[currentIndex].IsHittable)
                return enemies[currentIndex];
            currentIndex = (currentIndex + 1) % enemies.Count;
            searchCount++;
        }

        return null;
    }
}