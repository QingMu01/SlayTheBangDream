using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class Distance() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Common;
    private const TargetType CustomTarget = TargetType.None;

    public override bool GainsBlock => true;

    protected override IEnumerable<CardKeyword> CardKeywords => [];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        ComputedDynamicVarHelper.CreateBlockVar("CalcBlock", 0m, ctx =>
        {
            if (ctx.IsInCombat())
            {
                return ctx.ActiveCard.Owner.PlayerCombatState!.DiscardPile.Cards.Count;
            }

            return ctx.BaseValue;
        })
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var block = DynamicVars.ComputedValue("CalcBlock");
        if (block > 0)
        {
            await CreatureCmd.GainBlock(Owner.Creature, new BlockVar(block, ValueProp.Move), play);
        }

        await CardPileCmd.Shuffle(choiceContext, Owner);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}