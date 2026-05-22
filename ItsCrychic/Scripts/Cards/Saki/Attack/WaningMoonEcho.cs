using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using ItsCrychic.Scripts.Power.Temporary;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class WaningMoonEcho()
    : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), ISubsideCardFlag
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    public int LingeredEnergyCost => 4;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new DamageVar(10m, ValueProp.Move),
        new RepeatVar(6),
        new IntVar("DamageMultiplier", 4)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        await MusicNoteCmd.FromCard(choiceContext, this, baseCount: DynamicVars.Repeat.IntValue);
    }

    public async Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PowerCmd.Apply<WaningMoonEchoPower>(choiceContext, Owner.Creature,
            DynamicVars["DamageMultiplier"].IntValue,
            Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars.Repeat.UpgradeValueBy(2);
    }
}