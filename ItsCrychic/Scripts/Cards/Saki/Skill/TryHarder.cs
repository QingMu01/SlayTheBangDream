using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Cards.Token;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class TryHarder() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromCard<GiantNote>()
    ];

    protected override IEnumerable<DynamicVar> CardVars => [];


    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var extraDraw = BangDreamTools.GetPile(BangDreamConst.ExtraDraw, Owner);
        var cards = extraDraw.Cards.ToList();

        foreach (var card in cards)
        {
            await CardPileCmd.RemoveFromCombat(card);
        }

        foreach (var giantNote in cards.Select(_ => CombatState.CreateCard<GiantNote>(Owner)))
        {
            await CardPileCmd.AddGeneratedCardToCombat(giantNote, extraDraw.Type, Owner);
        }
    }
}