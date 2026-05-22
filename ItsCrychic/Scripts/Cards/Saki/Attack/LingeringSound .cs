using BangDreamLib.Scripts.Interfaces.CardAugment;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class LingeringSound()
    : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), ISubsideCardFlag
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.AllAllies;

    public int LingeredEnergyCost => 1;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new DamageVar(3m, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m);
    }

    public async Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (Pile?.Type != PileType.Hand)
        {
            await CardPileCmd.Add(this, PileType.Hand);
        }
    }
}