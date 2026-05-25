using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Power.Temporary;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Keywords;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class Distance() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Common;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.KeywordLinger.GetModCardKeyword()
    ];

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        ModCardVars.Computed(nameof(StrengthPower), 0m, card =>
                DynamicVarHelper.ResolveBaseVar(card, GetLingered),
            (card, _, _, _) => GetLingered(card))
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var strengthPower = this.DynamicVar<ComputedDynamicVar>(nameof(StrengthPower)).Calculate();
        if (strengthPower > 0)
        {
            var allCreatures = play.Card.CombatState?.Creatures.Where(item =>
                item is { IsPlayer: true, IsAlive: true } or { IsPlayer: false, IsHittable: true }).ToList();
            if (allCreatures is { Count: > 0 })
            {
                await PowerCmd.Apply<DistancePower>(choiceContext, allCreatures, strengthPower,
                    Owner.Creature, this);
            }
        }
    }

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (!IsUpgraded && card == this)
        {
            await CardCmd.AutoPlay(choiceContext, this, null);
        }
    }

    private static decimal GetLingered(CardModel? cardModel)
    {
        if (cardModel != null)
        {
            return cardModel.Owner.AttachedData().LingeredEnergy.Counter;
        }

        return 0m;
    }
}