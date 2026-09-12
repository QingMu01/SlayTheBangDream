using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class OctagramDance() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None),
    IPerformHookListener
{
    private bool _isReplayingAdjacent;

    protected override IEnumerable<DynamicVar> CardVars => [QuickVar.Cards.Create(2)];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        if (_isReplayingAdjacent) return;

        var manager = Owner.AttachedData().PerformManager;
        var slotIndex = manager.CardContexts.GetOrCreate(this).SlotIndex;
        var cardsBySlot = manager.PerformPile.Cards
            .Where(card => card != this)
            .Select(card => (card, manager.CardContexts.GetOrCreate(card).SlotIndex))
            .Where(entry => entry.Item2 >= 1)
            .ToDictionary(entry => entry.Item2, entry => entry.card);

        // 相邻位为同名牌时沿该方向继续寻找最近的非同名牌
        var adjacentCards = new List<CardModel>();
        foreach (var step in new[] { -1, 1 })
        {
            for (var slot = slotIndex + step; slot >= 1 && slot <= manager.Capacity; slot += step)
            {
                if (!cardsBySlot.TryGetValue(slot, out var card)) continue;

                if (card is OctagramDance) continue;

                adjacentCards.Add(card);
                break;
            }
        }

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