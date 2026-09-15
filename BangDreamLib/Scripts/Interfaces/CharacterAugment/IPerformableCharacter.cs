namespace BangDreamLib.Scripts.Interfaces.CharacterAugment;

/// <summary>
/// 可演奏角色。角色通过提供演奏方案决定其歌单的分组划分、容量上限与初值以及区域布局。
/// </summary>
public interface IPerformableCharacter : IExtraDeckSupportCharacter
{
    /// <summary>
    /// 创建本角色的演奏方案。方案为不可变静态策略，每次调用应返回等价内容；
    /// 每场战斗的可变容量由 <c>PerformCapacityState</c> 单独承载。
    /// </summary>
    IPerformScheme CreatePerformScheme();
}
