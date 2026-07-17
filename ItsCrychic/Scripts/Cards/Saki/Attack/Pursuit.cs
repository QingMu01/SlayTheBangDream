using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class Pursuit() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget),
    ICopySelfAndPlayFlag, ISubsideCard
{
    private const int CustomCost = 2;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    public int LingeredResourceCost => 6;

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
        BangDreamConst.Lingered
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(16),
        QuickVar.LingeredResource.Create(7)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        var attackCommand = await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, play)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        if (attackCommand.Results.SelectMany(r => r).Any(result => result.WasTargetKilled))
        {
            await SecondaryResourceCmd.Gain(Owner, BangDreamConst.LingeredResource,
                QuickVar.LingeredResource.GetVar(this).IntValue, this);
        }
    }

    public Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ShouldCopySelfAndPlayOnce = true;
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        this.SecondaryCosts().Set(BangDreamConst.LingeredResource, 4);
    }
}