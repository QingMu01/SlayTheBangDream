using MegaCrit.Sts2.Core.Rewards;
using STS2RitsuLib.Combat.Rewards;

namespace BangDreamLib.Scripts.Extensions;

public static class CustomRewardExtensions
{
    public static RewardType GetRewardType(this string rewardId)
    {
        return ModRewardRegistry.GetRewardType(rewardId);
    }
}