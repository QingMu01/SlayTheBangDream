using System.Reflection;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Relics;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Enums;
using ItsCrychic.Scripts.Cards.Mutsumi;
using ItsCrychic.Scripts.Cards.Saki;
using ItsCrychic.Scripts.Character;
using ItsCrychic.Scripts.Patches;
using ItsCrychic.Scripts.Relics.Mutsumi;
using ItsCrychic.Scripts.Relics.Sakiko;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models.RelicPools;
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

    private static readonly Dictionary<Type, Func<Type, bool>> CharacterContentFilter = new()
    {
        [typeof(TogawaSakiko)] = modelType => modelType.IsSubclassOf(typeof(AbstractSakikoCard)) ||
                                              modelType.IsSubclassOf(typeof(AbstractSakikoMusicCard)) ||
                                              modelType.IsSubclassOf(typeof(AbstractSakikoRelic)),

        [typeof(WakabaMutsumi)] = modelType => modelType.IsSubclassOf(typeof(AbstractMutsumiCard)) ||
                                               modelType.IsSubclassOf(typeof(AbstractMutsumiMusicCard)) ||
                                               modelType.IsSubclassOf(typeof(AbstractMutsumiRelic))
    };

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

        // 扫描需要注册到Pool中的Model
        var allCrychicModelsOrigin = BangDreamTools.CollectAllModels(executingAssembly);
        var allCrychicModels = new List<Type>(allCrychicModelsOrigin);
        // 注册祥子相关内容
        var sakikoContent = RitsuLibFramework.GetContentRegistry(CrychicConst.ModId);
        sakikoContent.RegisterCharacter<TogawaSakiko>();
        allCrychicModels.RemoveAll(type =>
        {
            if (CharacterContentFilter.TryGetValue(typeof(TogawaSakiko), out var filter) && filter(type))
            {
                return BangDreamRegisterHelper.GetRegisterType(type).RegisterContent(type, sakikoContent);
            }

            return false;
        });
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
        allCrychicModels.RemoveAll(type =>
        {
            if (CharacterContentFilter.TryGetValue(typeof(WakabaMutsumi), out var filter) && filter(type))
            {
                return BangDreamRegisterHelper.GetRegisterType(type).RegisterContent(type, mutsumiContent);
            }

            return false;
        });
        mutsumiContent.RegisterCharacterStarterRelic<WakabaMutsumi, Synthesizer>();

        // 注册隐藏遗物
        var hiddenRelic = RitsuLibFramework.GetContentRegistry(CrychicConst.ModId);
        allCrychicModels.RemoveAll(type =>
        {
            if (type.IsSubclassOf(typeof(HiddenRelic)))
            {
                hiddenRelic.RegisterRelic(typeof(DeprecatedRelicPool), type);
                return true;
            }

            return false;
        });

        // 注册公共内容
        var commonContent = RitsuLibFramework.GetContentRegistry(CrychicConst.ModId);
        foreach (var type in allCrychicModels)
        {
            BangDreamRegisterHelper.GetRegisterType(type).RegisterContent(type, commonContent);
        }

        CharacterGroup.Crychic.SetBandSelectIcon("res://ItsCrychic/images/charui/char_select_saki.png");
    }
}