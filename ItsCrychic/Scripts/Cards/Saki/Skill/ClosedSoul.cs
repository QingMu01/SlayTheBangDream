using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class ClosedSoul() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), ISubsideCard
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Basic;
    private const TargetType CustomTarget = TargetType.Self;

    public int LingeredEnergyCost => 3;

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Block.Create(6),
        QuickVar.Block.Create("ExtraBlock", 3)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
    }

    public async Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, (BlockVar)DynamicVars["ExtraBlock"], play);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Block"].UpgradeValueBy(2m);
        DynamicVars["ExtraBlock"].UpgradeValueBy(1m);
    }
}