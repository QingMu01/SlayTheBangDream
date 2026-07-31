using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Utils;

public static class BangDreamPreloadManager
{
    private const string MainMenuSessionName = "BangDreamMainMenu";
    private const string CombatSessionName = "BangDreamCombat";
    private static readonly TimeSpan ThreadedLoadPollDelay = TimeSpan.FromMilliseconds(15);

    private static readonly Lock RegistrationLock = new();
    private static readonly Lock CacheLock = new();
    private static readonly Lock TransitionStateLock = new();
    private static readonly SemaphoreSlim AssetTransitionLock = new(1, 1);

    private static readonly Dictionary<string, string> CustomCommonAssets = new();
    private static readonly Dictionary<string, string> CustomCombatAssets = new();
    private static readonly Dictionary<string, Resource> CachedAssets = new();
    private static IReadOnlySet<string> _preparedCombatVisualAssets = new HashSet<string>();
    private static CancellationTokenSource? _activeAssetTransition;

    internal static readonly Dictionary<PreloadKey, string> SceneAssets = new()
    {
        { PreloadKey.PerformItem, "res://BangDreamLib/scenes/perform_item.tscn" },
        { PreloadKey.PerformArea, "res://BangDreamLib/scenes/perform_area.tscn" },
        { PreloadKey.LingeredOrbitVfx, "res://BangDreamLib/scenes/vfx/lingered_orbit_vfx.tscn" },
        { PreloadKey.CharacterSelector, "res://BangDreamLib/scenes/character_selector/character_selector.tscn" },
        { PreloadKey.CharacterButton, "res://BangDreamLib/scenes/character_selector/character_button.tscn" },
        { PreloadKey.AscensionPanel, "res://BangDreamLib/scenes/character_selector/ascension_panel.tscn" },
        { PreloadKey.SkinSelector, "res://BangDreamLib/scenes/character_selector/skin_selector.tscn" },
        { PreloadKey.HeadTip, "res://BangDreamLib/scenes/head_tip.tscn" },
    };

    private static readonly HashSet<PreloadKey> MainMenuSceneKeys =
    [
        PreloadKey.CharacterSelector,
        PreloadKey.CharacterButton,
        PreloadKey.AscensionPanel,
        PreloadKey.SkinSelector
    ];

    private static readonly HashSet<PreloadKey> CombatSceneKeys =
    [
        PreloadKey.PerformItem,
        PreloadKey.PerformArea,
        PreloadKey.LingeredOrbitVfx
    ];

    private static readonly HashSet<string> CombatAssets =
    [
        "res://BangDreamLib/images/sceneui/default_portrait.png",
        "res://BangDreamLib/shaders/color_overlay.gdshader"
    ];

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
        var assets = GetScenePaths(MainMenuSceneKeys);
        lock (RegistrationLock)
        {
            assets.UnionWith(CustomCommonAssets.Values);
            _preparedCombatVisualAssets = new HashSet<string>();
        }

        foreach (var character in ModelDb.AllCharacters)
        {
            if (character is IBangDreamMateData mateData)
            {
                AddIfNotEmpty(assets, mateData.SelectPoster);
                AddIfNotEmpty(assets, mateData.SelectLogo);
            }

            if (character is IGroupableCharacter groupableCharacter)
            {
                AddIfNotEmpty(assets, groupableCharacter.Group.GetGroupSelectIcon());
            }

            if (character is not ISkinSupportCharacter skinSupportCharacter) continue;
            foreach (var skinPath in skinSupportCharacter.CharacterSkinList)
            {
                var visualScene = SkinManager.GetSkinInfo(skinPath)?.SkinTemplate.MultiplayerVisual.VisualScene;
                AddIfNotEmpty(assets, visualScene);
            }
        }

        await SwitchAssetSet(MainMenuSessionName, assets);
    }

    public static Task LoadCombatAssets(IEnumerable<Player> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        return SwitchAssetSet(CombatSessionName, GetCombatAssetPaths(GetPlayerVisualAssetPaths(players)));
    }

    public static Task LoadRunAssets(IEnumerable<Player> players)
    {
        return LoadCombatAssets(players);
    }

    internal static void PrepareCombatAssets(IEnumerable<Player> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        var visualAssets = GetPlayerVisualAssetPaths(players);
        lock (RegistrationLock)
        {
            _preparedCombatVisualAssets = visualAssets;
        }
    }

    internal static Task LoadPreparedCombatAssets()
    {
        IReadOnlySet<string> visualAssets;
        lock (RegistrationLock)
        {
            visualAssets = _preparedCombatVisualAssets;
        }

        return SwitchAssetSet(CombatSessionName, GetCombatAssetPaths(visualAssets));
    }

    private static HashSet<string> GetPlayerVisualAssetPaths(IEnumerable<Player> players)
    {
        var assets = new HashSet<string>();
        foreach (var player in players)
        {
            var currentSkin = BangDreamConst.PlayerSkin.Get(player).GetSkin();
            if (currentSkin != null)
            {
                assets.UnionWith(currentSkin.GetAllVisualResourcePaths());
            }
        }

        return assets;
    }

    public static void AddCustomCommonAsset(string name, string path)
    {
        lock (RegistrationLock)
        {
            CustomCommonAssets.Add(name, path);
        }
    }

    public static void AddCustomCombatAsset(string name, string path)
    {
        lock (RegistrationLock)
        {
            CustomCombatAssets.Add(name, path);
        }
    }

    public static void AddCustomRunAsset(string name, string path)
    {
        AddCustomCombatAsset(name, path);
    }

    public static T GetAsset<T>(string path) where T : Resource
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        lock (CacheLock)
        {
            if (CachedAssets.TryGetValue(path, out var cachedAsset))
            {
                return CastAsset<T>(path, cachedAsset);
            }
        }

        var loadedAsset = ResourceLoader.Load<T>(path, cacheMode: ResourceLoader.CacheMode.IgnoreDeep) ??
                          throw new InvalidOperationException($"Failed to load mod resource: {path}");
        lock (CacheLock)
        {
            if (CachedAssets.TryGetValue(path, out var cachedAsset))
            {
                return CastAsset<T>(path, cachedAsset);
            }

            CachedAssets[path] = loadedAsset;
            return loadedAsset;
        }
    }

    public static PackedScene GetScene(string path)
    {
        return GetAsset<PackedScene>(path);
    }

    public static Texture2D GetTexture2D(string path)
    {
        return GetAsset<Texture2D>(path);
    }

    public static Material GetMaterial(string path)
    {
        return GetAsset<Material>(path);
    }

    public static Shader GetShader(string path)
    {
        return GetAsset<Shader>(path);
    }

    private static HashSet<string> GetCombatAssetPaths(IEnumerable<string> playerVisualAssets)
    {
        var assets = GetScenePaths(CombatSceneKeys);
        assets.UnionWith(VfxAssets);
        assets.UnionWith(CombatAssets);
        assets.UnionWith(playerVisualAssets);
        lock (RegistrationLock)
        {
            assets.UnionWith(CustomCombatAssets.Values);
        }

        return assets;
    }

    private static HashSet<string> GetScenePaths(IEnumerable<PreloadKey> keys)
    {
        return keys.Select(key => SceneAssets[key]).ToHashSet();
    }

    private static void AddIfNotEmpty(HashSet<string> assets, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            assets.Add(path);
        }
    }

    private static async Task SwitchAssetSet(string sessionName, HashSet<string> desiredAssets)
    {
        var transitionCancellation = BeginAssetTransition();
        var transitionLockTaken = false;
        try
        {
            await AssetTransitionLock.WaitAsync(transitionCancellation.Token);
            transitionLockTaken = true;

            ReleaseUnusedAssets(desiredAssets);
            foreach (var path in desiredAssets)
            {
                transitionCancellation.Token.ThrowIfCancellationRequested();
                if (IsCached(path)) continue;
                await LoadAssetInBackground(path, sessionName, transitionCancellation.Token);
            }
        }
        catch (OperationCanceledException) when (transitionCancellation.IsCancellationRequested)
        {
            // A newer asset set superseded this transition.
        }
        finally
        {
            if (transitionLockTaken)
            {
                AssetTransitionLock.Release();
            }

            lock (TransitionStateLock)
            {
                if (ReferenceEquals(_activeAssetTransition, transitionCancellation))
                {
                    _activeAssetTransition = null;
                }
            }
        }
    }

    private static CancellationTokenSource BeginAssetTransition()
    {
        var transitionCancellation = new CancellationTokenSource();
        CancellationTokenSource? previousTransition;
        lock (TransitionStateLock)
        {
            previousTransition = _activeAssetTransition;
            _activeAssetTransition = transitionCancellation;
        }

        previousTransition?.Cancel();
        return transitionCancellation;
    }

    private static bool IsCached(string path)
    {
        lock (CacheLock)
        {
            return CachedAssets.ContainsKey(path);
        }
    }

    private static void ReleaseUnusedAssets(IReadOnlySet<string> desiredAssets)
    {
        lock (CacheLock)
        {
            foreach (var path in CachedAssets.Keys.Where(path => !desiredAssets.Contains(path)).ToArray())
            {
                CachedAssets.Remove(path);
            }
        }
    }

    private static async Task LoadAssetInBackground(
        string path,
        string sessionName,
        CancellationToken cancellationToken)
    {
        var status = ResourceLoader.LoadThreadedGetStatus(path);
        if (status is not ResourceLoader.ThreadLoadStatus.InProgress and
            not ResourceLoader.ThreadLoadStatus.Loaded)
        {
            var error = ResourceLoader.LoadThreadedRequest(
                path,
                useSubThreads: false,
                cacheMode: ResourceLoader.CacheMode.IgnoreDeep);
            if (error != Error.Ok)
            {
                status = ResourceLoader.LoadThreadedGetStatus(path);
                if (status is not ResourceLoader.ThreadLoadStatus.InProgress and
                    not ResourceLoader.ThreadLoadStatus.Loaded)
                {
                    BangDreamLibCore.Logger.Error(
                        $"Failed to request mod resource for {sessionName}: {path} ({error})");
                    return;
                }
            }
            else
            {
                status = ResourceLoader.LoadThreadedGetStatus(path);
            }
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (status)
            {
                case ResourceLoader.ThreadLoadStatus.Loaded:
                    var resource = ResourceLoader.LoadThreadedGet(path);
                    if (resource == null)
                    {
                        BangDreamLibCore.Logger.Error($"Loaded mod resource is null: {path}");
                        return;
                    }

                    lock (CacheLock)
                    {
                        CachedAssets.TryAdd(path, resource);
                    }

                    return;
                case ResourceLoader.ThreadLoadStatus.Failed:
                case ResourceLoader.ThreadLoadStatus.InvalidResource:
                    BangDreamLibCore.Logger.Error($"Failed to load mod resource for {sessionName}: {path}");
                    return;
                case ResourceLoader.ThreadLoadStatus.InProgress:
                    await WaitForNextLoadPoll(cancellationToken);
                    status = ResourceLoader.LoadThreadedGetStatus(path);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private static Task WaitForNextLoadPoll(CancellationToken cancellationToken)
    {
        return Task.Delay(ThreadedLoadPollDelay, cancellationToken);
    }

    private static T CastAsset<T>(string path, Resource resource) where T : Resource
    {
        return resource as T ?? throw new InvalidCastException(
            $"Cached mod resource {path} is {resource.GetType().Name}, not {typeof(T).Name}.");
    }
}

public enum PreloadKey
{
    PerformItem,
    PerformArea,
    LingeredOrbitVfx,
    CharacterSelector,
    CharacterButton,
    AscensionPanel,
    SkinSelector,
    HeadTip
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
            ? BangDreamPreloadManager.GetScene(path)
            : throw new ArgumentOutOfRangeException(nameof(key), $"Key {key} not found");
    }
}
