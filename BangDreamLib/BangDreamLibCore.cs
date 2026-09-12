using System.Reflection;
using BangDreamLib.Scripts.Character;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Mechanics;
using BangDreamLib.Scripts.Multiplayer.RunData;
using BangDreamLib.Scripts.Patches;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.RunData;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace BangDreamLib;

[ModInitializer(nameof(Initialize))]
public static class BangDreamLibCore
{
    public static readonly Logger Logger = RitsuLibFramework.CreateLogger(BangDreamConst.ModId);

    public static void Initialize()
    {
        var executingAssembly = Assembly.GetExecutingAssembly();
        RitsuLibFramework.EnsureGodotScriptsRegistered(executingAssembly, Logger);
        ModTypeDiscoveryHub.RegisterModAssembly(BangDreamConst.ModId, executingAssembly);

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
        BangDreamMechanicRegistry.Discover(executingAssembly);
        var context = new BangDreamMechanicContext(BangDreamConst.ModId, runData);
        foreach (var mechanic in BangDreamMechanicRegistry.Mechanics)
        {
            mechanic.RegisterContent(context);

            var patcher = RitsuLibFramework.CreatePatcher(BangDreamConst.ModId, mechanic.Id);
            mechanic.RegisterPatches(patcher);
            if (!patcher.PatchAll())
            {
                throw new InvalidOperationException($"mechanic '{mechanic.Id}' patches failed.");
            }
        }

        RegisterInfrastructurePatches();

        // 注册公共内容
        var commonContent = RitsuLibFramework.GetContentRegistry(BangDreamConst.ModId);
        commonContent.RegisterCharacter<GroupCharacterPlaceholder>();
        commonContent.RegisterCharacterStarterRelic<GroupCharacterPlaceholder, Circlet>();

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
