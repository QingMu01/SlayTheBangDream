using BangDreamLib.Scripts.Interfaces.CardAugment;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class ClosedSoul() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), ISubsideCardFlag
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Basic;
    private const TargetType CustomTarget = TargetType.Self;

    public int LingeredEnergyCost => 3;

    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new BlockVar("Block", 6m, ValueProp.Move),
        new BlockVar("ExtraBlock", 3m, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, (BlockVar)DynamicVars["Block"], play);
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