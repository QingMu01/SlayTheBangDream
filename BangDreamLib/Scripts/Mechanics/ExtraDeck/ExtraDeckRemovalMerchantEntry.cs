using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamLib.Scripts.Mechanics.ExtraDeck;

/// <summary>
/// 小祥商人的删牌服务：付费从玩家的额外卡组中移除一张牌，每次使用后价格提高。
/// </summary>
public sealed class ExtraDeckRemovalMerchantEntry : MerchantEntry
{
    private static int BaseCost => AscensionHelper.GetValueIfAscension(AscensionLevel.Inflation, 100, 75);

    /// <summary>
    /// 每次使用删牌服务后的价格增量。
    /// </summary>
    public static int PriceIncrease => AscensionHelper.GetValueIfAscension(AscensionLevel.Inflation, 50, 25);

    public bool Used { get; private set; }

    public override bool IsStocked => !Used;

    public ExtraDeckRemovalMerchantEntry(Player player) : base(player)
    {
        CalcCost();
    }

    public override void CalcCost()
    {
        var removalsUsed = BangDreamConst.ExtraCardMerchant.Get(_player).CardRemovalsUsed;
        _cost = BaseCost + PriceIncrease * removalsUsed;
    }

    public void SetUsed()
    {
        Used = true;
    }

    protected override async Task<(bool, int)> OnTryPurchase(MerchantInventory? inventory, bool ignoreCost)
    {
        if (Used)
        {
            return (false, 0);
        }

        var goldToSpend = ignoreCost ? 0 : Cost;
        var prefs = new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1)
        {
            Cancelable = true,
            RequireManualConfirmation = true
        };
        var card = (await ExtraPileCmd.FromExtraDeckForRemoval(_player, prefs)).FirstOrDefault();
        if (card == null)
        {
            return (false, 0);
        }

        if (goldToSpend > 0)
        {
            await PlayerCmd.LoseGold(goldToSpend, _player, GoldLossType.Spent);
        }

        await ExtraPileCmd.RemoveFromExtraDeck(card);
        BangDreamConst.ExtraCardMerchant.Modify(_player, data => data.CardRemovalsUsed++);
        SetUsed();
        return (true, goldToSpend);
    }

    protected override void ClearAfterPurchase()
    {
    }

    protected override void RestockAfterPurchase(MerchantInventory? inventory)
    {
    }
}
