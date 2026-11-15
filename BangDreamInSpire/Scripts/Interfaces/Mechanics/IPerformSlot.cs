using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Interfaces.Mechanics;

/// <summary>
/// 演奏槽位的只读视图契约。机制层通过它触发槽位表现，不直接依赖 <c>NPerformItem</c>。
/// </summary>
public interface IPerformSlot
{
    /// <summary>槽位当前承载的卡牌；空槽为 <see langword="null" />。</summary>
    CardModel? Model { get; }

    /// <summary>卡图揭示、飞牌等待或入场弹跳仍在进行。</summary>
    bool IsArrivalVisualPlaying { get; }

    /// <summary>本次入场已播放其它反馈，关闭重复的扫光。</summary>
    void SuppressPortraitSweep();

    /// <summary>播放卡图揭示动画。</summary>
    void PlayPortraitReveal(float duration = 0.5f);
}
