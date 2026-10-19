using System.Reflection;
using BangDreamInSpire.Scripts.Character;
using BangDreamInSpire.Scripts.Diagnostics;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Mechanics;
using BangDreamInSpire.Scripts.Multiplayer.RunData;
using BangDreamInSpire.Scripts.Patches;
using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.RunData;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace BangDreamInSpire;

[ModInitializer(nameof(Initialize))]
public static class BangDreamInSpireCore
{
    public static readonly Logger Logger = RitsuLibFramework.CreateLogger(BangDreamConst.ModId);

    public static void Initialize()
    {
        // 诊断开关是持久化设置，且只在启动时读取一次（设置页改动需重启生效）。
        PerfConfig perfConfig = RegisterPerfSettings();
        PerfSession.Initialize(Logger, perfConfig);
        RegisterPerfSettingsPage();

        PerfTimeline.Measure("mod:BangDreamInSpire.Initialize", InitializeCore);
        PerfTimeline.Mark("mod:init:end");
    }

    /// <summary>注册并读取 BDS-Perf 的 Global 数据槽（关闭时也注册，玩家才能从设置页打开）。</summary>
    private static PerfConfig RegisterPerfSettings()
    {
        try
        {
            using (RitsuLibFramework.BeginModDataRegistration(BangDreamConst.ModId))
            {
                RitsuLibFramework.GetDataStore(BangDreamConst.ModId).Register(
                    key: PerfConfig.DataKey,
                    fileName: "perf_settings.json",
                    scope: SaveScope.Global,
                    syncToCloud: false,
                    defaultFactory: () => new PerfConfig(),
                    autoCreateIfMissing: true);
            }

            return RitsuLibFramework.GetDataStore(BangDreamConst.ModId).Get<PerfConfig>(PerfConfig.DataKey);
        }
        catch (Exception ex)
        {
            Logger.Warn($"[BDS-Perf] 读取诊断设置失败，按关闭处理: {ex.Message}");
            return new PerfConfig();
        }
    }

    /// <summary>在 RitsuLib 设置页提供唯一的开关；改动需要重启游戏才会生效。</summary>
    private static void RegisterPerfSettingsPage()
    {
        RitsuLibFramework.RegisterModSettings(BangDreamConst.ModId, page => page
            .WithTitle(ModSettingsText.Literal("BangDreamInSpire"))
            .WithModDisplayName(ModSettingsText.Literal("BangDreamInSpire"))
            .AddSection("diagnostics", section => section
                .WithTitle(ModSettingsText.Literal("诊断"))
                .AddToggle(
                    "perf_diagnostics_enabled",
                    ModSettingsText.Literal("性能诊断"),
                    new ModSettingsValueBinding<PerfConfig, bool>(
                        BangDreamConst.ModId,
                        PerfConfig.DataKey,
                        SaveScope.Global,
                        config => config.Enabled,
                        (config, value) => config.Enabled = value),
                    ModSettingsText.Literal("记录启动/卡顿诊断报告（logs/bangdream，可随 getlogs 一起打包）。修改后需重启游戏生效。"))
                .AddParagraph(
                    "perf_diagnostics_hint",
                    ModSettingsText.Literal("说明"),
                    ModSettingsText.Literal("默认关闭，关闭时不加载任何采样逻辑。玩家反馈卡顿时打开本项并重启游戏，复现后执行 getlogs 把 zip 发给作者。"))));
    }

    private static void InitializeCore()
    {
        var executingAssembly = Assembly.GetExecutingAssembly();
        PerfTimeline.Measure("mod:godot-scripts", () =>
            RitsuLibFramework.EnsureGodotScriptsRegistered(executingAssembly, Logger));
        PerfTimeline.Measure("mod:mod-type-discovery", () =>
            ModTypeDiscoveryHub.RegisterModAssembly(BangDreamConst.ModId, executingAssembly));

        using var runDataRegistration = RitsuLibFramework.BeginModDataRegistration(BangDreamConst.ModId);
        var runData = RitsuLibFramework.GetRunSavedDataStore(BangDreamConst.ModId);

        // 玩家皮肤属于跨机制的通用基础设施，保留在核心注册。
        BangDreamConst.PlayerSkin = runData.RegisterPerPlayer(
            key: BangDreamConst.RunDataKeySkin,
            defaultFactory: () => new PlayerSkinData(),
            options: new RunSavedDataOptions
            {
                WritePolicy = RunSavedDataWritePolicy.AlwaysWhenRegistered,
                SyncLobbyOnChange = true
            });

        // 机制模块：发现 -> 内容注册 -> 补丁注入。新增机制无需改动本文件。
        PerfTimeline.Measure("mod:mechanic-discovery", () => BangDreamMechanicRegistry.Discover(executingAssembly));
        var context = new BangDreamMechanicContext(BangDreamConst.ModId, runData);
        foreach (var mechanic in BangDreamMechanicRegistry.Mechanics)
        {
            var currentMechanic = mechanic;
            PerfTimeline.Measure($"mechanic:{currentMechanic.Id}:content", () =>
                currentMechanic.RegisterContent(context));

            var patcher = RitsuLibFramework.CreatePatcher(BangDreamConst.ModId, currentMechanic.Id);
            PerfTimeline.Measure($"mechanic:{currentMechanic.Id}:patches", () =>
            {
                currentMechanic.RegisterPatches(patcher);
                if (!patcher.PatchAll())
                {
                    throw new InvalidOperationException($"mechanic '{currentMechanic.Id}' patches failed.");
                }
            });
        }

        PerfTimeline.Measure("mod:infrastructure-patches", RegisterInfrastructurePatches);

        // 注册公共内容
        var commonContent = RitsuLibFramework.GetContentRegistry(BangDreamConst.ModId);
        PerfTimeline.Measure("mod:common-content", () =>
        {
            commonContent.RegisterCharacter<GroupCharacterPlaceholder>();
            commonContent.RegisterCharacterStarterRelic<GroupCharacterPlaceholder, Circlet>();
        });

        // 预加载皮肤资源
        RitsuLibFramework.SubscribeLifecycle<ModelPreloadingCompletedEvent>(_ =>
        {
            foreach (var character in ModelDb.AllCharacters.OfType<ISkinSupportCharacter>())
            {
                foreach (var skinPath in character.CharacterSkinList)
                {
                    var skinTemplate = BangDreamTools.LoadFromJson<SkinTemplate>(skinPath);
                    if (skinTemplate != null)
                    {
                        SkinManager.RegisterCharacterSkin(skinPath, skinTemplate);
                    }
                    else
                    {
                        Logger.Error($"Failed to load skin template from {skinPath}");
                    }
                }
            }
        });

        // 机制战斗生命周期订阅
        RitsuLibFramework.SubscribeLifecycle<CombatStartingEvent>(ctx =>
        {
            if (ctx.CombatState?.Players == null) return;

            foreach (var player in ctx.CombatState.Players)
            {
                BangDreamMechanicRegistry.SubmitCombatState(player);
            }
        });

        RitsuLibFramework.SubscribeLifecycle<CombatEndedEvent>(ctx =>
        {
            if (ctx.CombatState?.Players == null) return;

            foreach (var player in ctx.CombatState.Players)
            {
                BangDreamMechanicRegistry.UnsubscribeCombatState(player);
            }
        });

        ModHelper.SubscribeForCombatStateHooks("ExtraSubscribe",
            state => BangDreamMechanicRegistry.GetCombatHookModels(state.Players).ToList());
    }

    /// <summary>
    /// 注册与具体玩法机制无关的基础设施补丁：资源预加载、角色选择器、皮肤视觉与初始内容、历史记录等。
    /// </summary>
    private static void RegisterInfrastructurePatches()
    {
        // 纯观测补丁：仅在诊断开启时注册；关闭时完全不 patch，保证零影响。
        if (PerfSession.Enabled)
        {
            var perfDiagnostics = RitsuLibFramework.CreatePatcher(BangDreamConst.ModId, "perf_diagnostics");
            perfDiagnostics.RegisterPatches<PerfAssetPatches>();
            bool perfProbesApplied = false;
            PerfTimeline.Measure("mod:perf-probes", () => perfProbesApplied = perfDiagnostics.PatchAll());
            PerfTimeline.Mark("mod:perf-probes-applied", perfProbesApplied ? "true" : "false");
        }

        var preload = RitsuLibFramework.CreatePatcher(BangDreamConst.ModId, "preload_assets");
        preload.RegisterPatches<PreloadPatches>();
        preload.PatchAll();

        var submenuSupport = RitsuLibFramework.CreatePatcher(BangDreamConst.ModId, "character_selector_submenu");
        submenuSupport.RegisterPatches<GroupableCharacterSelectorPatches>();
        if (!submenuSupport.PatchAll())
        {
            throw new InvalidOperationException("character selector submenu patches failed.");
        }

        var skinSupport = RitsuLibFramework.CreatePatcher(BangDreamConst.ModId, "skin_support");
        skinSupport.RegisterPatches<SkinVisualSupportPatches>();
        skinSupport.RegisterPatches<SkinStartingContentPopulatePatches>();
        if (!skinSupport.PatchAll())
        {
            throw new InvalidOperationException("skins support patches failed.");
        }

        var runHistoryPatcher = RitsuLibFramework.CreatePatcher(BangDreamConst.ModId, "run_history_extra_deck");
        runHistoryPatcher.RegisterPatches<ExtraDeckRunHistoryPatches>();
        runHistoryPatcher.PatchAll();

        var commonPatcher = RitsuLibFramework.CreatePatcher(BangDreamConst.ModId, "common_patch");
        commonPatcher.RegisterPatch<CardHoverTipPatch>();
        commonPatcher.RegisterPatch<MainMenuEnvironmentCharacterPatch>();
        commonPatcher.PatchAll();
    }
}
