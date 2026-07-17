using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.GameHook;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class HachimanguseiDance() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None),
    IPerformHookListener
{
    private bool _isReplayingAdjacent;

    protected override IEnumerable<DynamicVar> CardVars => [QuickVar.Cards.Create(2)];

    public override async Task OnPerform(PlayerChoiceContext choiceContext)
    {
        if (_isReplayingAdjacent) return;

        var manager = Owner.AttachedData().PerformManager;
        var slotIndex = manager.CardContexts.GetOrCreate(this).SlotIndex;
        var adjacentCards = manager.PerformPile.Cards
            .Where(card => card != this && Math.Abs(manager.CardContexts.GetOrCreate(card).SlotIndex - slotIndex) == 1)
            .ToList();

        _isReplayingAdjacent = true;
        try
        {
            foreach (var card in adjacentCards)
            {
                await manager.PerformCard(card);
            }
        }
        finally
        {
            _isReplayingAdjacent = false;
        }
    }

    public async Task OnCardEnterPerformArea(PlayerChoiceContext choiceContext, CardModel cardModel)
    {
        if (IsUpgraded && cardModel == this)
        {
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
        }
    }
}