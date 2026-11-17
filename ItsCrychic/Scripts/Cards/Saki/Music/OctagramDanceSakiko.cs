using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.GameHook;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class OctagramDanceSakiko() : AbstractSakikoMusicCard(CustomRarity, CustomTarget),
    IPerformHookListener
{
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private static readonly TargetType CustomTarget = PerformTargetTypes.RequestGroup;

    private bool _isReplayingAdjacent;

    protected override IEnumerable<DynamicVar> CardVars => [QuickVar.Cards.Create(2)];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        if (_isReplayingAdjacent) return;

        var manager = Owner.AttachedData().Perform;
        var slotIndex = manager.GetSlotIndex(this);
        // 同一槽位出现两张牌是异常态；ToDictionary 会直接抛，这里按首次出现保留即可。
        var cardsBySlot = new Dictionary<int, CardModel>();
        foreach (var card in manager.PerformPile.Cards)
        {
            if (card == this) continue;

            var slot = manager.GetSlotIndex(card);
            if (slot >= 1)
            {
                cardsBySlot.TryAdd(slot, card);
            }
        }

        // 相邻位为同名牌时沿该方向继续寻找最近的非同名牌
        var adjacentCards = new List<CardModel>();
        foreach (var step in new[] { -1, 1 })
        {
            for (var slot = slotIndex + step; slot >= 1 && slot <= manager.Capacity; slot += step)
            {
                if (!cardsBySlot.TryGetValue(slot, out var card)) continue;

                if (card is OctagramDanceSakiko) continue;

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