using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Cards.Token;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class CherishedMemories() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.None;

    public override bool GainsBlock => true;

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromCard<MemoryPuzzle>()
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Block.Create(3)
    ];
    
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var hand = PileType.Hand.GetPile(Owner);
        var handCards = hand.Cards.ToList();
        var discardedCount = handCards.Count;

        foreach (var card in handCards)
        {
            await CardPileCmd.Add(card, PileType.Discard);
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        }

        var generatedCards = new List<CardModel>();
        for (var i = 0; i < discardedCount; i++)
        {
            generatedCards.Add(CombatState.CreateCard<MemoryPuzzle>(Owner));
        }

        await CardPileCmd.AddGeneratedCardsToCombat(generatedCards, BangDreamConst.PileExtraDraw.GetPileType(),
            Owner, CardPilePosition.Top);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(1m);
    }
}