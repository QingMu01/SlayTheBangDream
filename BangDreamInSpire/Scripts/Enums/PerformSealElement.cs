namespace BangDreamInSpire.Scripts.Enums;

/// <summary>
/// 演奏印框的元素墨色。每种墨色对应一套 preset 场景（同一个 <c>PerformSealVfx</c> 骨架，
/// 只是配色、节拍与墨迹不同），所以新增元素 = 加场景 + 加枚举值，不改流程。
/// </summary>
public enum PerformSealElement
{
    /// <summary>火：上舔的火舌与上升余烬。</summary>
    Fire,

    /// <summary>风：向两端甩出的长丝与风幕。</summary>
    Wind,

    /// <summary>水：先溅起再滴落的水珠与水雾。</summary>
    Water,

    /// <summary>自然/土：斜上掠过的草刃与土屑。</summary>
    Earth,

    /// <summary>群星/宇宙：逐点亮起的四芒星与星尘。</summary>
    Stars
}
