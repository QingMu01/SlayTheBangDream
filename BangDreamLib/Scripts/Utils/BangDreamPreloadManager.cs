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
        { PreloadKey.PerformanceManager, "res://BangDreamLib/scenes/performance_manager.tscn" },
        { PreloadKey.CharacterSelector, "res://BangDreamLib/scenes/character_selector.tscn" },
        { PreloadKey.CharacterButton, "res://BangDreamLib/scenes/character_button.tscn" }
    };

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

            if (!string.IsNullOrEmpty(character.SelectIcon))
            {
                hashSet.Add(character.SelectIcon);
            }
        }

        await LoadAssetSets("BangDreamCommon", hashSet);
    }

    public static async Task LoadRunAssets(IEnumerable<Player> players)
    {
        var allAssets = new HashSet<string>();
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

    private static async Task<AssetLoadingSession> LoadAssetSets(string name, params IEnumerable<string>[] assetSets)
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
        var assetLoadingSession = LoadAssets(needLoad, name);
        return assetLoadingSession;
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
    PerformanceManager,
    CharacterSelector,
    CharacterButton
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