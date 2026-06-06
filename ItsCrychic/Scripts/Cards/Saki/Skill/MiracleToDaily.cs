using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class MiracleToDaily() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 2;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.Self;

    public override bool GainsBlock => true;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.PerformanceArea
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Block.Create(16)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);

        var performanceCards = BangDreamTools.GetPile(BangDreamConst.PerformanceTable, Owner).Cards.ToList();
        foreach (var performanceCard in performanceCards)
        {
            var card = CardFactory.GetDistinctForCombat(Owner,
                ModelDb.CardPool<ColorlessCardPool>()
                    .GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint), 1,
                Owner.RunState.Rng.CombatCardGeneration).FirstOrDefault();
            if (card == null) continue;

            if (IsUpgraded) CardCmd.Upgrade(card);

            await CardCmd.Transform(performanceCard, card);
            await CardPileCmd.Add(performanceCard, PileType.Hand);
        }
    }
}