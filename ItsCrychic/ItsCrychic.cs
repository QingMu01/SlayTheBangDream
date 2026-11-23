using System.Reflection;
using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.Mechanics;
using BangDreamInSpire.Scripts.Utils;
using ItsCrychic.Scripts.Character;
using ItsCrychic.Scripts.Nodes.VFX;
using ItsCrychic.Scripts.Relics.Sakiko;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models.Relics;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using STS2RitsuLib.Scaffolding.Content;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace ItsCrychic;

[ModInitializer(nameof(Initialize))]
public static class ItsCrychic
{
    public static readonly Logger Logger = RitsuLibFramework.CreateLogger(CrychicConst.ModId);

    public static void Initialize()
    {
        var executingAssembly = Assembly.GetExecutingAssembly();
        RitsuLibFramework.EnsureGodotScriptsRegistered(executingAssembly, Logger);
        ModTypeDiscoveryHub.RegisterModAssembly(CrychicConst.ModId, executingAssembly);

        var sakikoContent = RitsuLibFramework.GetContentRegistry(CrychicConst.ModId);
        sakikoContent.RegisterCharacter<TogawaSakiko>();
        sakikoContent.RegisterCharacterStarterRelic<TogawaSakiko, Synthesizer>();
        sakikoContent.RegisterCharacterOwnedRelicVisualOverride<TogawaSakiko, YummyCookie>(new RelicAssetProfile
        {
            BigIconPath = "res://ItsCrychic/images/relics/big/CookieSakiko.png",
            IconPath = "res://ItsCrychic/images/relics/CookieSakiko.png",
            IconOutlinePath = "res://ItsCrychic/images/relics/big/CookieSakiko.png"
        });

        var mutsumiContent = RitsuLibFramework.GetContentRegistry(CrychicConst.ModId);
        mutsumiContent.RegisterCharacter<WakabaMutsumi>();
        mutsumiContent.RegisterCharacterStarterRelic<WakabaMutsumi, Synthesizer>();
        CharacterGroup.Crychic.SetBandSelectIcon("res://ItsCrychic/images/charui/char_select_saki.png");

        // 角色选择与能量图标加入对应预载集，避免首次使用时在主线程同步加载。
        BangDreamPreloadManager.AddCustomCommonAsset("crychic_group_select_icon",
            "res://ItsCrychic/images/charui/char_select_saki.png");
        BangDreamPreloadManager.AddCustomCombatAsset("sakiko_small_energy",
            "res://ItsCrychic/images/charui/sakiko/sakiko_small_energy.png");

        // 角色专属资源由内容模组注册，核心预载表不引用具体角色资源。
        BangDreamPreloadManager.AddCustomCombatAsset("sakiko_strike_vfx",
            "res://ItsCrychic/scenes/vfx/strike_sakiko_vfx.tscn");
        BangDreamPreloadManager.AddCustomCombatAsset("sakiko_card_trail",
            "res://BangDreamInSpire/scenes/vfx/card_trail.tscn");
        BangDreamPreloadManager.AddCustomCombatAsset("sakiko_lingered_energy_small",
            "res://BangDreamInSpire/images/sceneui/xz-energy_lingered_small.png");
        BangDreamPreloadManager.AddCustomCombatAsset("sakiko_lingered_energy_large",
            "res://BangDreamInSpire/images/sceneui/xz-energy_lingered.png");
        BangDreamPreloadManager.AddCustomCommonAsset("sakiko_night",
            "res://ItsCrychic/images/sceneui/sakiko_night.png");

        // 主菜单日夜贴图由内容模组配置，核心不引用角色资源。
        BangDreamInSpire.Scripts.Nodes.MainMenu.MainMenuEnvironmentCharacter.ConfigureEnvironmentTextures(
            "res://ItsCrychic/images/sceneui/sakiko_day.png",
            "res://ItsCrychic/images/sceneui/sakiko_night.png");

        // 演奏区冻结与分组激活由内容模组实现，核心仅提供数据与注册入口。
        PerformAreaVisuals.RegisterFreezeVisual(static () => new MutsumiPlaylistFreezeVfx
        {
            Name = MutsumiPlaylistFreezeVfx.NodeName,
            ZIndex = 4
        });
        PerformAreaVisuals.RegisterActivationVisual(static () => new MutsumiGroupActivationVfx
        {
            Name = MutsumiGroupActivationVfx.NodeName,
            ZIndex = 7
        });
    }
}