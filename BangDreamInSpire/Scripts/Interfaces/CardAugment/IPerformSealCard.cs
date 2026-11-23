using BangDreamInSpire.Scripts.Enums;

namespace BangDreamInSpire.Scripts.Interfaces.CardAugment;

/// <summary>
/// 演奏时改用专属"元素印"播放反馈，而不是通用的扫光。
/// 只声明元素；场景路径由核心的 <c>PerformSealScenes</c> 统一维护，内容模组不持有资源路径。
/// </summary>
public interface IPerformSealCard
{
    /// <summary>本牌演奏时使用的元素墨色。</summary>
    PerformSealElement PerformSeal { get; }
}
