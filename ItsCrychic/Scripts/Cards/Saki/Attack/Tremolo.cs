using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.AttackHits;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class Tremolo() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), IAttackHitHookListener
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.AllEnemies;

    protected override bool HasEnergyCostX => true;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new DamageVar(5m, ValueProp.Move)
    ];

    private readonly List<Creature> _enemiesSnapshot = [];
    private bool _isFinalBlast;

    public Task BeforeAttackHit(AttackHitContext context)
    {
        if (context.CardSource == this && !_isFinalBlast)
        {
            var nextTarget = GetNextTarget(_enemiesSnapshot, context.HitIndex);
            if (nextTarget != null)
            {
                context.Targets = new List<Creature> { nextTarget };
            }
        }

        return Task.CompletedTask;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var hitCount = ResolveEnergyXValue();

        if (hitCount > 0)
        {
            _enemiesSnapshot.Clear();
            _enemiesSnapshot.AddRange(CombatState.HittableEnemies.ToList());

            if (_enemiesSnapshot.Count > 0)
            {
                var attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                    .FromCard(this, play)
                    .TargetingAllOpponents(CombatState)
                    .WithHitCount(hitCount)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(choiceContext);

                var unblockedDamage = attack.Results.SelectMany(results => results)
                    .Sum(result => result.UnblockedDamage);
                if (unblockedDamage > 0)
                {
                    _isFinalBlast = true;
                    try
                    {
                        await DamageCmd.Attack(unblockedDamage)
                            .FromCard(this, play)
                            .TargetingAllOpponents(CombatState)
                            .WithHitFx("vfx/vfx_attack_slash")
                            .Execute(choiceContext);
                    }
                    finally
                    {
                        _isFinalBlast = false;
                    }
                }
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }

    private static Creature? GetNextTarget(List<Creature> enemies, int startIndex)
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
            currentIndex %= enemies.Count;
            if (enemies[currentIndex].IsHittable)
                return enemies[currentIndex];
            currentIndex++;
            searchCount++;
        }

        return null;
    }
}
