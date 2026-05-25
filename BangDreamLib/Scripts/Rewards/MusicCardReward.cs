using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace BangDreamLib.Scripts.Rewards;

public class MusicCardReward : CardReward
{
    public override LocString Description => new("gameplay_ui", "BANG_DREAM_LIB_COMBAT_REWARD_ADD_MUSIC_CARD");
    protected override string IconPath => "res://BangDreamLib/images/sceneui/music_reward.png";

    protected override RewardType RewardType => BangDreamConst.RewardMusic.GetRewardType();

    public MusicCardReward(CardCreationOptions options, int cardCount, Player player,
        PlayerChoiceSynchronizer? synchronizer = null) : base(options, cardCount, player, synchronizer)
    {
    }

    public MusicCardReward(IEnumerable<CardModel> cardsToOffer, CardCreationSource source, Player player,
        CardCreationOptions rerollOptions, PlayerChoiceSynchronizer? synchronizer = null) : base(cardsToOffer, source,
        player, rerollOptions, synchronizer)
    {
    }

    public override void Populate()
    {
        base.Populate();
        foreach (var cardModel in Cards)
        {
            IPerformanceCard.CardEnterExtraDeck.Set(cardModel, true);
        }
    }

    public override SerializableReward ToSerializable()
    {
        var serializableReward = base.ToSerializable();
        serializableReward.RewardType = RewardType;
        return serializableReward;
    }
}