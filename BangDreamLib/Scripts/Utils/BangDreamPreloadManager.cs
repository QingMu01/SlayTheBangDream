using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Utils;

public static class BangDreamPreloadManager
{
    private static readonly Lock RegistrationLock = new();
    private static readonly Dictionary<string, string> CustomCommonAssets = new();
    private static readonly Dictionary<string, string> CustomCombatAssets = new();
    private static IReadOnlySet<string> _preparedCombatVisualAssets = new HashSet<string>();
    private static AssetScope _currentScope = AssetScope.MainMenu;

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
        { PreloadKey.MainMenuEnvironmentCharacter, "res://BangDreamLib/scenes/main_menu/environment_character.tscn" },
    };

    private static readonly HashSet<PreloadKey> MainMenuSceneKeys =
    [
        PreloadKey.CharacterSelector,
        PreloadKey.CharacterButton,
        PreloadKey.AscensionPanel,
        PreloadKey.SkinSelector,
        PreloadKey.MainMenuEnvironmentCharacter
    ];

    private static readonly HashSet<PreloadKey> CombatSceneKeys =
    [
        PreloadKey.PerformItem,
        PreloadKey.PerformArea,
        PreloadKey.LingeredOrbitVfx,
        PreloadKey.HeadTip
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
        "res://BangDreamLib/scenes/vfx/music_equalizer_vfx.tscn",
        "res://BangDreamLib/scenes/vfx/perform_flash_vfx.tscn"
    ];

    internal static void PrepareCombatAssets(IEnumerable<Player> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        var visualAssets = GetPlayerVisualAssetPaths(players);
        lock (RegistrationLock)
        {
            _preparedCombatVisualAssets = visualAssets;
            _currentScope = AssetScope.Run;
        }
    }

    internal static IReadOnlySet<string> GetAssetsForSession(string sessionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionName);

        if (sessionName is "Common" or "MainMenu")
        {
            SetMainMenuScope();
            return GetMainMenuAssetPaths();
        }

        lock (RegistrationLock)
        {
            if (sessionName.StartsWith("characters=", StringComparison.Ordinal))
            {
                _currentScope = AssetScope.Run;
            }

            if (_currentScope != AssetScope.Run)
            {
                return new HashSet<string>();
            }
        }

        return GetRunAssetPaths();
    }

    public static void AddCustomCommonAsset(string name, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        lock (RegistrationLock)
        {
            CustomCommonAssets.Add(name, path);
        }
    }

    public static void AddCustomCombatAsset(string name, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        lock (RegistrationLock)
        {
            CustomCombatAssets.Add(name, path);
        }
    }
    
    public static T GetAsset<T>(string path) where T : Resource
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return PreloadManager.Cache.GetAsset<T>(path);
    }

    public static PackedScene GetScene(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return PreloadManager.Cache.GetScene(path);
    }

    public static Texture2D GetTexture2D(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return PreloadManager.Cache.GetTexture2D(path);
    }

    public static Material GetMaterial(string path)
    {
        return GetAsset<Material>(path);
    }

    public static Shader GetShader(string path)
    {
        return GetAsset<Shader>(path);
    }

    private static HashSet<string> GetMainMenuAssetPaths()
    {
        var assets = GetScenePaths(MainMenuSceneKeys);
        lock (RegistrationLock)
        {
            assets.UnionWith(CustomCommonAssets.Values);
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

            if (character is not ISkinSupportCharacter skinSupportCharacter)
            {
                continue;
            }

            foreach (var skinPath in skinSupportCharacter.CharacterSkinList)
            {
                var visualScene = SkinManager.GetSkinInfo(skinPath)?.SkinTemplate.MultiplayerVisual.VisualScene;
                AddIfNotEmpty(assets, visualScene);
            }
        }

        return assets;
    }

    private static HashSet<string> GetRunAssetPaths()
    {
        IReadOnlySet<string> visualAssets;
        lock (RegistrationLock)
        {
            visualAssets = _preparedCombatVisualAssets;
        }

        var assets = GetScenePaths(CombatSceneKeys);
        assets.UnionWith(VfxAssets);
        assets.UnionWith(CombatAssets);
        assets.UnionWith(visualAssets);
        lock (RegistrationLock)
        {
            assets.UnionWith(CustomCombatAssets.Values);
        }

        return assets;
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

    private static void SetMainMenuScope()
    {
        lock (RegistrationLock)
        {
            _currentScope = AssetScope.MainMenu;
            _preparedCombatVisualAssets = new HashSet<string>();
        }
    }

    private enum AssetScope
    {
        MainMenu,
        Run
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
    HeadTip,
    MainMenuEnvironmentCharacter
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
