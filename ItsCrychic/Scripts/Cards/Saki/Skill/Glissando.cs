using BangDreamLib.Scripts.Interfaces.CardAugment;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class Glissando() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), ISubsideCardFlag
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.Self;

    public int LingeredEnergyCost => 1;

    public override bool GainsBlock => true;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Ethereal
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new BlockVar(1m, ValueProp.Move),
        new CardsVar(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);

        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
    }

    public async Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CardPileCmd.Add(this, PileType.Hand);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(1m);
    }
}