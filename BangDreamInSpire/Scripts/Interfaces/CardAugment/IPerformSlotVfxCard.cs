namespace BangDreamInSpire.Scripts.Interfaces.CardAugment;

/// <summary>
/// 演奏时改用自定义的槽位反馈，而不是通用的扫光。
/// 牌只声明一个键；键到场景的映射由 <c>PerformSlotVfxRegistry</c> 维护，
/// 因此内容模组既可以用内置的元素之印，也可以登记自己的场景，且不必持有核心资源路径。
/// </summary>
public interface IPerformSlotVfxCard
{
    /// <summary>
    /// 演奏反馈的登记键；为空或未登记时回落通用扫光。
    /// 建议用 <c>模组.用途</c> 形式的键（内置元素见 <c>PerformSlotVfxKeys</c>）。
    /// </summary>
    string? PerformSlotVfxKey { get; }
}
