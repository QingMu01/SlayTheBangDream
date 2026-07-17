using BangDreamLib.Scripts.Commands;
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
        new DamageVar(7m, ValueProp.Move)
    ];

    private readonly List<Creature> _enemiesSnapshot = [];
    private readonly HashSet<Creature> _hitEnemies = [];

    public async Task BeforeAttackHit(AttackHitContext context)
    {
        if (context.CardSource == this)
        {
            var nextTarget = GetNextTarget(_enemiesSnapshot, context.HitIndex);
            if (nextTarget != null)
            {
                if (_hitEnemies.Add(nextTarget))
                {
                    await ExtraPileCmd.Draw(context.ChoiceContext, 1, Owner);
                }

                context.Targets = new List<Creature> { nextTarget };
            }
        }
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
                await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                    .FromCard(this, play)
                    .TargetingAllOpponents(CombatState)
                    .WithHitCount(hitCount)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(choiceContext);
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
            searchCount++;
        }

        return null;
    }
}