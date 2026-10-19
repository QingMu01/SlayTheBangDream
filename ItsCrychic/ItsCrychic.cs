using System.Reflection;
using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Utils;
using ItsCrychic.Scripts.Character;
using ItsCrychic.Scripts.Patches;
using ItsCrychic.Scripts.Relics.Sakiko;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models.Relics;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using STS2RitsuLib.Patching.Core;
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

        //Patches
        var skinSupport = RitsuLibFramework.CreatePatcher(CrychicConst.ModId, "setup_sakiko_lingered_energy_counter");
        skinSupport.RegisterPatch<SakikoLingeredEnergyCounterPatch>();
        if (!skinSupport.PatchAll())
        {
            throw new InvalidOperationException("sakiko lingered energy counter setup failed.");
        }

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
    }
}