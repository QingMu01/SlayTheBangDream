using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class DivineWrath() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget),
    ICopySelfAndPlayFlag
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    private bool _shouldCopySelfAndPlay;

    public bool ShouldCopySelfAndPlayOnce
    {
        get => _shouldCopySelfAndPlay;
        set
        {
            AssertMutable();
            _shouldCopySelfAndPlay = value;
        }
    }

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.Linger
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(8),
        QuickVar.LingeredEnergy.Create(2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        await LingeredCmd.AddLeByCard(this, DynamicVars["LingeredEnergy"].IntValue);

        if (Owner.RunState.Rng.CombatTargets.NextBool())
        {
            ShouldCopySelfAndPlayOnce = true;
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["LingeredEnergy"].UpgradeValueBy(1m);
    }
}