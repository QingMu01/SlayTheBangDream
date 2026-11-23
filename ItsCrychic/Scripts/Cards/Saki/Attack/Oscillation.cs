using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Nodes.VFX;
using BangDreamInSpire.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

[RegisterArchaicToothTranscendence(typeof(Surge))]
public class Oscillation()
    : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Basic;
    private const TargetType CustomTarget = TargetType.AllEnemies;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.Lingered
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(8),
        QuickVar.LingeredResource.Create(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var enemies = CombatState.HittableEnemies;
        var gainPerEnemy = QuickVar.LingeredResource.GetVar(this).IntValue;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, play)
            .TargetingAllOpponents(CombatState)
            .WithAttackerFx(() => OscillationWaveVfx.Create(Owner.Creature, enemies))
            .WithWaitBeforeHit(OscillationWaveVfx.FrontTravelSeconds * 0.85f,
                OscillationWaveVfx.FrontTravelSeconds)
            .WithHitVfxNode(target => OscillationHitVfx.Create(target, Owner.Creature, gainPerEnemy))
            .Execute(choiceContext);

        await SecondaryResourceCmd.Gain(Owner, BangDreamConst.LingeredResource,
            gainPerEnemy * CombatState.HittableEnemies.Count, this);
    }

    protected override void OnUpgrade()
    {
        QuickVar.LingeredResource.GetVar(this).UpgradeValueBy(1m);
    }
}
