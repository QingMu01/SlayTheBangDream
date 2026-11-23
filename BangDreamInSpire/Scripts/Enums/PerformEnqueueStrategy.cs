namespace BangDreamInSpire.Scripts.Enums;

public enum PerformEnqueueStrategy
{
    /// <summary>插入 Slot 1，其余牌上移，溢出牌离开。</summary>
    Default,

    /// <summary>强制进入目标槽位并替换占用牌，无效时回退 Default。</summary>
    Fixed,

    /// <summary>进入目标或最近空槽，无空槽时回退 Default。</summary>
    Nearby,

    /// <summary>优先向较小索引搜索空槽。</summary>
    Bottom,

    /// <summary>优先向较大索引搜索空槽。</summary>
    Top
}
