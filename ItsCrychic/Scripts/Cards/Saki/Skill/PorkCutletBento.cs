using BangDreamLib.Scripts.Extensions;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class PorkCutletBento() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 3;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.Self;

    public override bool GainsBlock => true;

    public override bool CanBeGeneratedInCombat => false;

    protected override bool IsPlayable => PileType.Hand.GetPile(Owner).Cards.All(card => card is not Enthralled);

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Heal.Create(10),
        QuickVar.Block.Create(10)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.BaseValue);
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
    }

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        if (card.Owner != Owner)
        {
            return true;
        }

        if (Pile is not { Type: PileType.Hand })
        {
            return true;
        }

        if (card is PorkCutletBento or Enthralled)
        {
            return true;
        }

        return autoPlayType != AutoPlayType.None;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Heal.UpgradeValueBy(2m);
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}