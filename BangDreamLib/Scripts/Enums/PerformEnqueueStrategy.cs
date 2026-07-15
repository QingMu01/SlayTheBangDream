namespace BangDreamLib.Scripts.Enums;

public enum PerformEnqueueStrategy
{
    /// <summary>
    /// 就近空位
    /// </summary>
    Nearby,

    /// <summary>
    /// 优先向下搜索
    /// </summary>
    Bottom,

    /// <summary>
    /// 优先向上搜索
    /// </summary>
    Top,

    /// <summary>
    /// 固定值
    /// </summary>
    Fixed
}