using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Extensions;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.RunData;

namespace BangDreamInSpire.Scripts.Mechanics.Puppeteer;

/// <summary>傀儡师机制，依赖演奏机制；战斗开始先建演奏区再恢复形态。</summary>
public sealed class PuppeteerMechanic : IBangDreamMechanic
{
    public string Id => "puppeteer";

    /// <summary>在演奏机制后装配。</summary>
    public int Order => 250;

    public IReadOnlyList<string> Dependencies => ["perform"];

    public IReadOnlyList<Type> RequiredCharacterCapabilities => [typeof(IPuppeteerCharacter)];

    public void RegisterContent(BangDreamMechanicContext context)
    {
        PuppeteerRunState.Bind(context.RunData.RegisterPerPlayer(
            key: PuppeteerRunState.RunDataKey,
            defaultFactory: () => new PuppeteerRunData(),
            options: new RunSavedDataOptions
            {
                WritePolicy = RunSavedDataWritePolicy.AlwaysWhenRegistered,
                SyncLobbyOnChange = true
            }));
    }

    /// <summary>无需 Harmony 补丁，全部使用模型钩子。</summary>
    public void RegisterPatches(ModPatcher patcher)
    {
    }

    public IEnumerable<AbstractModel> InstantiatePlayerState(Player player)
    {
        var manager = (PuppeteerFormManager)ModelDb.RunModel<PuppeteerFormManager>().MutableClone();
        manager.Player = player;
        yield return manager;
    }

    /// <summary>死亡拦截使用运行态订阅，确保先于原版 ShouldDie 遗物；傀儡死亡放行防死。</summary>
    public IEnumerable<AbstractModel> GetRunStateHookModels(Player player)
    {
        yield return player.AttachedData().PuppeteerFormManager;
    }

    public void SubmitCombatState(Player player)
    {
        player.AttachedData().PuppeteerFormManager.SubmitCombatState();
    }

    public void UnsubscribeCombatState(Player player)
    {
        player.AttachedData().PuppeteerFormManager.UnsubscribeCombatState();
    }
}
