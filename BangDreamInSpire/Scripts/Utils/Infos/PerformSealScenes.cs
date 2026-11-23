using BangDreamInSpire.Scripts.Enums;

namespace BangDreamInSpire.Scripts.Utils.Infos;

/// <summary>
/// 演奏印框的场景表：元素墨色 → 场景路径。
/// 放在 Utils 层，流程侧（演奏区节点）与内容模组（预载注册）都能引用，不必各自硬写路径。
/// </summary>
public static class PerformSealScenes
{
    private static readonly IReadOnlyDictionary<PerformSealElement, string> Paths =
        new Dictionary<PerformSealElement, string>
        {
            [PerformSealElement.Fire] = "res://BangDreamInSpire/scenes/vfx/perform_seal_fire_vfx.tscn",
            [PerformSealElement.Wind] = "res://BangDreamInSpire/scenes/vfx/perform_seal_wind_vfx.tscn",
            [PerformSealElement.Water] = "res://BangDreamInSpire/scenes/vfx/perform_seal_water_vfx.tscn",
            [PerformSealElement.Earth] = "res://BangDreamInSpire/scenes/vfx/perform_seal_earth_vfx.tscn",
            [PerformSealElement.Stars] = "res://BangDreamInSpire/scenes/vfx/perform_seal_stars_vfx.tscn"
        };

    /// <summary>全部印框场景，供战斗资源预载。</summary>
    public static IEnumerable<string> All => Paths.Values;

    /// <summary>取某元素墨色的场景路径；未登记时返回 null（调用方回落通用扫光）。</summary>
    public static string? ScenePathOf(PerformSealElement element) => Paths.GetValueOrDefault(element);
}
