using BangDreamInSpire.Scripts.Mechanics.ExtraDeck;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;

namespace BangDreamInSpire.Scripts.Extensions;

/// <summary>
/// 额外卡组奖励/休息处选项的稳定工厂。内容模组通过本类创建，不直接依赖 ExtraDeck 实现类。
/// </summary>
public static class ExtraDeckRewardExtensions
{
    /// <summary>创建音乐牌奖励。</summary>
    public static Reward CreateMusicCardReward(this Player player, CardCreationOptions options, int cardCount)
        => new MusicCardReward(options, cardCount, player);

    /// <summary>创建「技术练习」休息处选项。</summary>
    public static RestSiteOption CreateTechnicalPracticeOption(this Player player)
        => new TechnicalPracticeOption(player);
}
