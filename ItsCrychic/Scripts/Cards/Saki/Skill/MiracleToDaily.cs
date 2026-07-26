using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

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
        BangDreamConst.Instant,
        BangDreamConst.PerformArea,
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Block.Create(16)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);

        var performManager = Owner.AttachedData().PerformManager;
        var performCards = BangDreamConst.PerformPile.GetPile(Owner).Cards.ToList();

        var candidates = CardFactory.FilterForCombat(BangDreamTools.GetCharacterExtraCards(Owner))
            .Where(card => card is IPerformCard { IsInstant: true })
            .ToList();

        if (candidates.Count > 0)
        {
            foreach (var originalCard in performCards)
            {
                var candidate = Owner.RunState.Rng.CombatCardGeneration.NextItem(candidates);
                if (candidate == null) continue;

                var transformCard = CombatState.CreateCard(candidate, Owner);

                if (IsUpgraded) CardCmd.Upgrade(transformCard);

                // 在替换牌进入歌单前继承原牌槽位。
                var originalContext = performManager.CardContexts.GetOrCreate(originalCard);
                var replacementContext = performManager.CardContexts.GetOrCreate(transformCard);

                replacementContext.Manager = performManager;
                replacementContext.SlotIndex = originalContext.SlotIndex;

                var result = await CardCmd.Transform(originalCard, transformCard);
                if (result is not { success: true })
                {
                    performManager.CardContexts.Remove(transformCard);
                }
            }
        }
    }
}
