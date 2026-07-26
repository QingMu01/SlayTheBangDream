namespace BangDreamLib.Scripts.Enums;

public enum PerformEnqueueStrategy
{
    /// <summary>
    /// 插入歌单最下方的 Slot 1，并忽略期望槽位。
    /// 原有卡牌依次上移至最近的空槽位；没有空槽位时，超出容量的卡牌离开歌单。
    /// </summary>
    Default,

    /// <summary>
    /// 强制进入期望槽位；目标槽位中的卡牌会离开歌单。
    /// 期望槽位无效时回退到 <see cref="Default"/>。
    /// </summary>
    Fixed,

    /// <summary>
    /// 优先进入期望槽位；槽位被占用时寻找距离最近的空槽位。
    /// 没有空槽位时回退到 <see cref="Default"/>。
    /// </summary>
    Nearby,

    /// <summary>
    /// 与 <see cref="Nearby"/> 相同，但优先向歌单下方（较小的槽位索引）搜索。
    /// </summary>
    Bottom,

    /// <summary>
    /// 与 <see cref="Nearby"/> 相同，但优先向歌单上方（较大的槽位索引）搜索。
    /// </summary>
    Top
}
