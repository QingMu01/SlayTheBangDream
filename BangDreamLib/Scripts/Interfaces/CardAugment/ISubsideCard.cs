using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace BangDreamLib.Scripts.Interfaces.CardAugment;

public interface ISubsideCard
{
    /// <summary>
    /// 是否允许这张牌在打出后生成余音资源
    /// </summary>
    bool ShouldGenerateResources => false;

    /// <summary>
    /// 触发休止效果时所需的余音资源消耗
    /// </summary>
    int LingeredResourceCost { get; }

    /// <summary>
    /// 休止效果
    /// </summary>
    Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play);
}