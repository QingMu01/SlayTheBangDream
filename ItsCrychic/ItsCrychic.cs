using System.Reflection;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils.Enums;
using ItsCrychic.Scripts.Character;
using ItsCrychic.Scripts.Patches;
using ItsCrychic.Scripts.Relics.Sakiko;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models.Relics;
using STS2RitsuLib;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Scaffolding.Content;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace ItsCrychic;

[ModInitializer(nameof(Initialize))]
public class ItsCrychic
{
    public static readonly Logger Logger = RitsuLibFramework.CreateLogger(CrychicConst.ModId);

    public static void Initialize()
    {
        var executingAssembly = Assembly.GetExecutingAssembly();
        RitsuLibFramework.EnsureGodotScriptsRegistered(executingAssembly, Logger);

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
    }
}