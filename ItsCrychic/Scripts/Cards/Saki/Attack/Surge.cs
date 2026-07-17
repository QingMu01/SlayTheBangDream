using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Combat.AttackHits;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class Surge() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), IAttackHitHookListener
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Ancient;
    private const TargetType CustomTarget = TargetType.AllEnemies;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.Lingered
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(10),
        QuickVar.LingeredResource.Create(3)
    ];

    private readonly List<Creature> _enemiesSnapshot = [];

    public async Task BeforeAttackHit(AttackHitContext context)
    {
        if (context.CardSource == this)
        {
            await SecondaryResourceCmd.Gain(Owner, BangDreamConst.LingeredResource,
                QuickVar.LingeredResource.GetVar(this).IntValue, this);

            context.Targets = new List<Creature> { _enemiesSnapshot[context.HitIndex] };
            context.Damage *= (int)Math.Pow(2, context.HitIndex);
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        _enemiesSnapshot.Clear();
        _enemiesSnapshot.AddRange(CombatState.HittableEnemies.ToList());

        if (_enemiesSnapshot.Count > 0)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, play)
                .Targeting(_enemiesSnapshot[0])
                .WithHitCount(_enemiesSnapshot.Count)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}