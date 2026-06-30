using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class BestRegards() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), ISubsideCard
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Common;
    private const TargetType CustomTarget = TargetType.Self;

    public int LingeredEnergyCost => 2;

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Block.Create(7),
        QuickVar.Cards.Create(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
    }

    public async Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}