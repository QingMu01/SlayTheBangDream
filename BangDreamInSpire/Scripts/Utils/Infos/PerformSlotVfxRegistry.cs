using BangDreamInSpire.Scripts.Utils;

namespace BangDreamInSpire.Scripts.Utils.Infos;

/// <summary>
/// 演奏槽位反馈的登记表：键 → 场景路径。内容模组可登记自己的场景，不必改核心演奏流程。
/// 接入示例见 docs/mechanic-authoring.md 12.1。
/// </summary>
public static class PerformSlotVfxRegistry
{
    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, string> ScenePaths = new(StringComparer.Ordinal);
    private static readonly HashSet<string> PreloadRegistered = new(StringComparer.Ordinal);
    private static bool _builtInsRegistered;

    /// <summary>内置元素之印的场景表。</summary>
    private static readonly (string Key, string ScenePath)[] BuiltIns =
    [
        (PerformSlotVfxKeys.SymbolFire, "res://BangDreamInSpire/scenes/vfx/perform_seal_fire_vfx.tscn"),
        (PerformSlotVfxKeys.SymbolWind, "res://BangDreamInSpire/scenes/vfx/perform_seal_wind_vfx.tscn"),
        (PerformSlotVfxKeys.SymbolWater, "res://BangDreamInSpire/scenes/vfx/perform_seal_water_vfx.tscn"),
        (PerformSlotVfxKeys.SymbolEarth, "res://BangDreamInSpire/scenes/vfx/perform_seal_earth_vfx.tscn"),
        (PerformSlotVfxKeys.SymbolStars, "res://BangDreamInSpire/scenes/vfx/perform_seal_stars_vfx.tscn")
    ];

    /// <summary>已登记的键（只读快照，供诊断与自检）。</summary>
    public static IReadOnlyDictionary<string, string> Entries
    {
        get
        {
            lock (Gate)
            {
                return new Dictionary<string, string>(ScenePaths, StringComparer.Ordinal);
            }
        }
    }

    /// <summary>登记一个演奏反馈场景；键必须全局唯一，建议带模组前缀。</summary>
    /// <param name="key">卡牌声明的键。</param>
    /// <param name="scenePath">场景路径；根节点需实现 <see cref="Interfaces.Mechanics.IPerformSlotVfx" />。</param>
    /// <param name="preload">是否随战斗预载。</param>
    public static void Register(string key, string scenePath, bool preload = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenePath);

        lock (Gate)
        {
            if (!ScenePaths.TryAdd(key, scenePath))
            {
                throw new InvalidOperationException(
                    $"演奏反馈键 '{key}' 已被登记为 '{ScenePaths[key]}'。键必须全局唯一，" +
                    "请换一个带模组前缀的键。");
            }

            if (preload)
            {
                RequestPreload(key, scenePath);
            }
        }
    }

    /// <summary>登记核心内置的五个元素之印；重复调用安全。</summary>
    public static void RegisterBuiltIns()
    {
        lock (Gate)
        {
            if (_builtInsRegistered) return;
            _builtInsRegistered = true;
            foreach (var (key, scenePath) in BuiltIns)
            {
                if (!ScenePaths.TryAdd(key, scenePath)) continue;
                RequestPreload(key, scenePath);
            }
        }
    }

    /// <summary>解析键到场景路径；键为空或未登记返回 false（调用方回落通用扫光）。</summary>
    public static bool TryResolve(string? key, out string scenePath)
    {
        scenePath = string.Empty;
        if (string.IsNullOrWhiteSpace(key)) return false;

        lock (Gate)
        {
            if (!ScenePaths.TryGetValue(key, out var resolved)) return false;
            scenePath = resolved;
            return true;
        }
    }

    /// <summary>把场景加入战斗预载集；同一键只加一次。</summary>
    private static void RequestPreload(string key, string scenePath)
    {
        if (!PreloadRegistered.Add(key)) return;
        BangDreamPreloadManager.AddCustomCombatAsset($"perform_slot_vfx_{key}", scenePath);
    }
}
