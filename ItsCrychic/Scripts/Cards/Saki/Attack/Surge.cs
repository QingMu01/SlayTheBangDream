using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class Surge() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
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
        ModCardVars.Int("LingeredEnergy", 3),
        QuickVar.Damage.Create(10)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var enemies = CombatState.HittableEnemies.ToList();
        await SecondaryResourceCmd.Gain(Owner, BangDreamConst.LingeredResource,
            enemies.Count * DynamicVars["LingeredEnergy"].IntValue, this);

        for (var i = 0; i < enemies.Count; i++)
        {
            var damage = DynamicVars.Damage.BaseValue * (decimal)Math.Pow(2, i);
            await DamageCmd.Attack(damage)
                .FromCard(this, play)
                .Targeting(enemies[i])
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}