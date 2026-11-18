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

        // 注册祥子相关内容
        var sakikoContent = RitsuLibFramework.GetContentRegistry(CrychicConst.ModId);
        sakikoContent.RegisterCharacter<TogawaSakiko>();
        sakikoContent.RegisterCharacterStarterRelic<TogawaSakiko, Synthesizer>();
        sakikoContent.RegisterCharacterOwnedRelicVisualOverride<TogawaSakiko, YummyCookie>(new RelicAssetProfile
        {
            BigIconPath = "res://ItsCrychic/images/relics/big/CookieSakiko.png",
            IconPath = "res://ItsCrychic/images/relics/CookieSakiko.png",
            IconOutlinePath = "res://ItsCrychic/images/relics/big/CookieSakiko.png"
        });

        // 注册睦相关内容
        var mutsumiContent = RitsuLibFramework.GetContentRegistry(CrychicConst.ModId);
        mutsumiContent.RegisterCharacter<WakabaMutsumi>();
        mutsumiContent.RegisterCharacterStarterRelic<WakabaMutsumi, Synthesizer>();
        // 设置 Crychic 角色组图标
        CharacterGroup.Crychic.SetBandSelectIcon("res://ItsCrychic/images/charui/char_select_saki.png");

        // BDS-Perf 报告显示这些资源原先在首次使用时走主线程同步加载；并入对应的预载集，
        // 把首次触碰的卡顿挪到预加载阶段（角色选择图标进主菜单，能量图标进 run 资源集）。
        BangDreamPreloadManager.AddCustomCommonAsset("crychic_group_select_icon",
            "res://ItsCrychic/images/charui/char_select_saki.png");
        BangDreamPreloadManager.AddCustomCombatAsset("sakiko_small_energy",
            "res://ItsCrychic/images/charui/sakiko/sakiko_small_energy.png");

        // 角色专属资源从核心预载表移出，改由内容模组注册（核心库不得出现具体角色资源）；
        // 注册后与原先一样并入 run / 主菜单预载集，首次触碰不再走主线程同步加载。
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

        // 主菜单环境角色的日夜贴图（核心不写死具体角色资源）。
        BangDreamInSpire.Scripts.Nodes.MainMenu.MainMenuEnvironmentCharacter.ConfigureEnvironmentTextures(
            "res://ItsCrychic/images/sceneui/sakiko_day.png",
            "res://ItsCrychic/images/sceneui/sakiko_night.png");

        // 演奏区冻结 / 分组激活表现由内容模组实现；核心只提供数据与注册入口。
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