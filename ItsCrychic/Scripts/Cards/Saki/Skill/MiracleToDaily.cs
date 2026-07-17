using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

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
        BangDreamConst.PerformArea
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Block.Create(16)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);

        var manager = Owner.AttachedData().PerformManager;
        var performanceCards = manager.PerformPile.Cards.ToList();
        var candidates = Owner.Character is IExtraDeckSupportCharacter character
            ? character.ExtraCardPool.AllCards.OfType<IPerformCard>().Where(card => card.IsInstant)
                .Cast<CardModel>().ToList()
            : [];
        foreach (var performanceCard in performanceCards)
        {
            var replacement = CardFactory.GetForCombat(Owner, candidates, 1,
                Owner.RunState.Rng.CombatCardGeneration).FirstOrDefault();
            if (replacement == null) continue;

            if (IsUpgraded) CardCmd.Upgrade(replacement);

            var transformResult = await CardCmd.Transform(performanceCard, replacement);
            if (transformResult is { success: true })
            {
                await CardPileCmd.Add(transformResult.Value.cardAdded, BangDreamConst.PerformPile);
            }
        }
    }
}
