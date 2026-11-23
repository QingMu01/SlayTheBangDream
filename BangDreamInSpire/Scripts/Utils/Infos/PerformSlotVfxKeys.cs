namespace BangDreamInSpire.Scripts.Utils.Infos;

/// <summary>内置演奏反馈（元素之印）的登记键；由核心在演奏机制注册时预登记。</summary>
public static class PerformSlotVfxKeys
{
    /// <summary>火：上舔的火舌与上升余烬。</summary>
    public const string SymbolFire = "symbol.fire";

    /// <summary>风：向两端甩出的长丝与风幕。</summary>
    public const string SymbolWind = "symbol.wind";

    /// <summary>水：先溅起再滴落的水珠与水雾。</summary>
    public const string SymbolWater = "symbol.water";

    /// <summary>自然/土：斜上掠过的草刃与土屑。</summary>
    public const string SymbolEarth = "symbol.earth";

    /// <summary>群星/宇宙：逐点亮起的四芒星与星尘。</summary>
    public const string SymbolStars = "symbol.stars";

    /// <summary>全部内置键，供检查与批量预载。</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        SymbolFire,
        SymbolWind,
        SymbolWater,
        SymbolEarth,
        SymbolStars
    ];
}
