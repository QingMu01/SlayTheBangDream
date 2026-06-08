using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;

namespace BangDreamLib.Scripts.Utils;

public static class BangDreamPreloadManager
{
    private static readonly Dictionary<string, string> CustomCommonAssets = new();
    private static readonly Dictionary<string, string> CustomRunAssets = new();

    internal static readonly Dictionary<PreloadKey, string> SceneAssets = new()
    {
        { PreloadKey.PerformanceItem, "res://BangDreamLib/scenes/performance_item.tscn" },
        { PreloadKey.PerformanceArea, "res://BangDreamLib/scenes/performance_area.tscn" },
        { PreloadKey.CharacterSelector, "res://BangDreamLib/scenes/character_selector/character_selector.tscn" },
        { PreloadKey.CharacterButton, "res://BangDreamLib/scenes/character_selector/character_button.tscn" },
        { PreloadKey.AscensionPanel, "res://BangDreamLib/scenes/character_selector/ascension_panel.tscn" },
        { PreloadKey.SkinSelector, "res://BangDreamLib/scenes/character_selector/skin_selector.tscn" }
    };

    internal static readonly HashSet<string> VfxAssets =
    [
        "res://BangDreamLib/scenes/vfx/staff_ring_vfx.tscn",
        "res://BangDreamLib/scenes/vfx/music_wave.tscn",
        "res://BangDreamLib/scenes/vfx/music_hit_vfx.tscn",
        "res://BangDreamLib/scenes/vfx/music_flash_vfx.tscn",
        "res://BangDreamLib/scenes/vfx/music_equalizer_vfx.tscn"
    ];

    public static async Task LoadCommonAssets()
    {
        var hashSet = new HashSet<string>();
        hashSet.UnionWith(SceneAssets.Values);
        hashSet.UnionWith(CustomCommonAssets.Values);
        foreach (var character in ModelDb.AllCharacters.OfType<IAggregationCharacter>().ToList())
        {
            if (!string.IsNullOrEmpty(character.SelectPoster))
            {
                hashSet.Add(character.SelectPoster);
            }
        }

        await LoadAssetSets("BangDreamCommon", hashSet);
    }

    public static async Task LoadRunAssets(IEnumerable<Player> players)
    {
        var allAssets = new HashSet<string>(VfxAssets);
        allAssets.UnionWith(CustomRunAssets.Values);
        foreach (var player in players)
        {
            var skinManagerCurrentSkin = player.AttachedData().SkinManager.CurrentSkin;
            if (skinManagerCurrentSkin != null)
            {
                allAssets.UnionWith(skinManagerCurrentSkin.GetAllVisualResourcePaths());
            }
        }

        await LoadAssetSets("BangDreamRun", allAssets);
    }

    public static void AddCustomCommonAsset(string name, string path)
    {
        CustomCommonAssets.Add(name, path);
    }

    public static void AddCustomRunAsset(string name, string path)
    {
        CustomRunAssets.Add(name, path);
    }

    private static async Task LoadAssetSets(string name, params IEnumerable<string>[] assetSets)
    {
        var assetPath = new HashSet<string>();
        foreach (var assetSet in assetSets)
        {
            foreach (var path in assetSet)
            {
                assetPath.Add(path);
            }
        }

        var loadedCacheAssets = PreloadManager.Cache.GetLoadedCacheAssets();
        var needLoad = assetPath.Except(loadedCacheAssets);
        await Task.Yield();
        _ = LoadAssets(needLoad, name);
    }

    private static AssetLoadingSession LoadAssets(IEnumerable<string> assetPaths, string name)
    {
        var session = PreloadManager.Cache.CreateSession(name, assetPaths);
        NAssetLoader.Instance.LoadInTheBackground(session);
        return session;
    }
}

public enum PreloadKey
{
    PerformanceItem,
    PerformanceArea,
    CharacterSelector,
    CharacterButton,
    AscensionPanel,
    SkinSelector,
}

public static class PreloadKeyExtensions
{
    public static string? GetPath(this PreloadKey key)
    {
        return BangDreamPreloadManager.SceneAssets.GetValueOrDefault(key);
    }

    public static PackedScene GetScene(this PreloadKey key)
    {
        var path = BangDreamPreloadManager.SceneAssets.GetValueOrDefault(key);
        return path != null
            ? PreloadManager.Cache.GetScene(path)
            : throw new ArgumentOutOfRangeException(nameof(key), $"Key {key} not found");
    }
}