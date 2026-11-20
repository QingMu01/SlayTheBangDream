using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Utils;

public static class BangDreamPreloadManager
{
    private static readonly Lock RegistrationLock = new();
    private static readonly Dictionary<string, string> CustomCommonAssets = new();
    private static readonly Dictionary<string, string> CustomCombatAssets = new();
    private static IReadOnlySet<string> _preparedCombatVisualAssets = new HashSet<string>();
    private static AssetScope _currentScope = AssetScope.MainMenu;

    internal static readonly Dictionary<PreloadKey, string> SceneAssets = new()
    {
        { PreloadKey.PerformItem, "res://BangDreamInSpire/scenes/perform_item.tscn" },
        { PreloadKey.PerformArea, "res://BangDreamInSpire/scenes/perform_area.tscn" },
        { PreloadKey.LingeredOrbitVfx, "res://BangDreamInSpire/scenes/vfx/lingered_orbit_vfx.tscn" },
        { PreloadKey.CharacterSelector, "res://BangDreamInSpire/scenes/character_selector/character_selector.tscn" },
        { PreloadKey.CharacterButton, "res://BangDreamInSpire/scenes/character_selector/character_button.tscn" },
        { PreloadKey.AscensionPanel, "res://BangDreamInSpire/scenes/character_selector/ascension_panel.tscn" },
        { PreloadKey.SkinSelector, "res://BangDreamInSpire/scenes/character_selector/skin_selector.tscn" },
        { PreloadKey.HeadTip, "res://BangDreamInSpire/scenes/head_tip.tscn" },
        { PreloadKey.MainMenuEnvironmentCharacter, "res://BangDreamInSpire/scenes/main_menu/environment_character.tscn" },
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
        "res://BangDreamInSpire/images/sceneui/default_portrait.png",
        "res://BangDreamInSpire/shaders/color_overlay.gdshader",
        "res://BangDreamInSpire/scenes/merchant/extra_card_merchant_inventory.tscn",
        // 战斗资源随房间预载，避免首次使用时在主线程同步加载。
        "res://BangDreamInSpire/scenes/vfx/oscillation_wave_vfx.tscn",
        "res://BangDreamInSpire/scenes/vfx/oscillation_hit_vfx.tscn",
        "res://BangDreamInSpire/images/sceneui/music_reward.png",
        // 睦的演奏区特效贴图：首次冻结 / 分组激活时同步读盘，提前放进 run 预载集。
        "res://BangDreamInSpire/images/vfx/flying_music_note/glow_soft.png",
        "res://BangDreamInSpire/images/vfx/whiteRing.png",
        "res://BangDreamInSpire/images/vfx/star_322.png",
        "res://BangDreamInSpire/images/vfx/star_270_1.png"
    ];

    /// <summary>
    /// 主菜单界面资源在 logo 播放期间预载，避免创建角色选择器等界面时同步加载。
    /// <para>
    /// 角色专属资源不写在这里：核心库不得出现具体角色标识，由内容模组通过
    /// <see cref="AddCustomCommonAsset" /> 注册后并入同一预载集。
    /// </para>
    /// </summary>
    private static readonly HashSet<string> MainMenuEssentialAssets = [];

    internal static readonly HashSet<string> VfxAssets =
    [
        // staff_ring / music_equalizer 只被彼此引用、无实例化点，预载它们等于白占 run 预算；
        // 场景文件本身按 keep-list 保留（编辑器/预览仍可用）。
        "res://BangDreamInSpire/scenes/vfx/perform_flash_vfx.tscn"
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

        if (sessionName is "MainMenuEssentials")
        {
            SetMainMenuScope();
            return GetMainMenuEssentialPaths();
        }

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

    /// <summary>
    /// MainMenuEssentials 会话用：只放主菜单创建阶段真正会用到的资源。
    /// 主菜单创建发生在 Common 预载之前，所以这些资源必须提前到 logo 播放期间预载。
    /// </summary>
    private static HashSet<string> GetMainMenuEssentialPaths()
    {
        var assets = GetScenePaths(MainMenuSceneKeys);
        assets.UnionWith(MainMenuEssentialAssets);
        lock (RegistrationLock)
        {
            assets.UnionWith(CustomCommonAssets.Values);
        }

        return assets;
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
            var currentSkin = SkinManager.ResolvePlayerSkin(player);
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
