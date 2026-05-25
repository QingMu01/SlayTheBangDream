using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Keywords;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class Pursuit() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget),
    ICopySelfAndPlayFlag, ISubsideCardFlag
{
    private const int CustomCost = 2;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    private int _lingeredEnergy = 6;

    public int LingeredEnergyCost
    {
        get => _lingeredEnergy;
        private set
        {
            AssertMutable();
            _lingeredEnergy = value;
        }
    }

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

    public bool IgnoreSubsideCost => false;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.KeywordLinger.GetModCardKeyword()
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new DamageVar(16m, ValueProp.Move),
        new IntVar("Gain", 7)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        var attackCommand = await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        if (attackCommand.Results.SelectMany(r => r).Any(result => result.WasTargetKilled))
        {
            await LingeredCmd.AddLeByCard(this, DynamicVars["Gain"].IntValue);
        }
    }

    public Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ShouldCopySelfAndPlayOnce = true;
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        LingeredEnergyCost = 4;
        DynamicVars.VarOrNull<DynamicVar>("Subside")?.UpgradeValueBy(-2m);
    }
}