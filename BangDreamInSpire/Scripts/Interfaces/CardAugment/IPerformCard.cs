using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Scaffolding.Content;

namespace BangDreamInSpire.Scripts.Interfaces.CardAugment;

public interface IPerformCard
{
    /// <summary>
    /// 即兴标记
    /// </summary>
    bool IsInstant { get; }

    /// <summary>
    /// 期望入队位置
    /// </summary>
    int AspirationSlot { get; }

    /// <summary>
    /// 入队策略
    /// </summary>
    PerformEnqueueStrategy Strategy { get; }

    /// <summary>
    /// 音乐牌的默认视觉资源。皮肤配置只负责运行时替换纹理，这里是"未配置皮肤/资源缺失"时唯一兜底：
    /// 返回值必须带非空 <see cref="CardAssetProfile.FramePath"/>，否则卡框会落回本体按 CardType 穷举的分支并抛异常。
    /// </summary>
    CardAssetProfile PerformCardAssets { get; }

    /// <summary>
    /// 决定演奏中的牌被丢弃时候的牌堆
    /// </summary>
    CardLocation StopPerformanceNextPile();

    Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform);
}