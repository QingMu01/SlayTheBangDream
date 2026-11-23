namespace BangDreamInSpire.Scripts.Interfaces.CardAugment;

/// <summary>演奏时改用自定义槽位反馈；牌只声明键，键到场景的映射见 <c>PerformSlotVfxRegistry</c>。</summary>
public interface IPerformSlotVfxCard
{
    /// <summary>登记键；为空或未登记时回落通用扫光。</summary>
    string? PerformSlotVfxKey { get; }
}
