using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace BangDreamInSpire.Scripts.Mechanics.ExtraDeck;

/// <summary>商人音乐牌条目；由专用槽位绑定，不进入 MerchantInventory.AllEntries。</summary>
public sealed class MusicCardMerchantEntry(
    Player player,
    CardPoolModel cardPool,
    CardRarity rarity,
    ISet<CardModel> duplicateGuard)
    : MerchantEntry(player)
{
    private const float PriceMultiplier = 1.2f;

    public CardCreationResult? CreationResult { get; private set; }

    public override bool IsStocked => CreationResult != null;

    public void Populate()
    {
        var options = cardPool
            .GetUnlockedCards(_player.UnlockState, _player.RunState.CardMultiplayerConstraint)
            .Where(card => !duplicateGuard.Contains(card.CanonicalInstance))
            .ToList();
        CreationResult = CardFactory.CreateForMerchant(_player, options, [], rarity);
        duplicateGuard.Add(CreationResult.Card.CanonicalInstance);
        var results = new List<CardCreationResult> { CreationResult };
        Hook.ModifyMerchantCardCreationResults(_player.RunState, _player, results);
        CalcCost();
    }

    protected override void UpdateEntry()
    {
        if (CreationResult == null)
        {
            return;
        }

        var results = new List<CardCreationResult> { CreationResult };
        Hook.ModifyMerchantCardCreationResults(_player.RunState, _player, results);
    }

    public override void CalcCost()
    {
        if (CreationResult == null)
        {
            throw new InvalidOperationException("There is no item to purchase.");
        }

        var baseCost = CreationResult.Card.Rarity switch
        {
            CardRarity.Rare => 150,
            CardRarity.Uncommon => 75,
            _ => 50,
        };
        _cost = Mathf.RoundToInt(baseCost * PriceMultiplier * _player.PlayerRng.Shops.NextFloat(0.95f, 1.05f));
    }

    protected override async Task<(bool, int)> OnTryPurchaseInternal(MerchantInventory? inventory, bool ignoreCost)
    {
        var addResult = await CardPileCmd.Add(CreationResult!.Card, BangDreamConst.ExtraDeck);
        if (!addResult.success)
        {
            InvokePurchaseFailed(PurchaseStatus.FailureSpace);
            return (false, 0);
        }

        if (!ignoreCost)
        {
            await PlayerCmd.LoseGold(Cost, _player, GoldLossType.Spent);
        }

        RunManager.Instance.RewardSynchronizer.SyncLocalGoldLost(Cost);
        var obtainedCard = addResult.cardAdded;
        obtainedCard.FloorAddedToDeck = _player.RunState.TotalFloor;
        _player.RunState.CurrentMapPointHistoryEntry?.GetEntry(_player.NetId)
            .CardsGained.Add(obtainedCard.ToSerializable());
        return (true, ignoreCost ? 0 : Cost);
    }

    protected override void ClearAfterPurchase()
    {
        CreationResult = null;
    }

    protected override void RestockAfterPurchase(MerchantInventory? inventory)
    {
        // 条目不在库存列表，此属性不使用。
        Populate();
    }
}
