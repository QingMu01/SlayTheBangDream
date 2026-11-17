using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Extensions;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.RunData;

namespace BangDreamInSpire.Scripts.Mechanics.Puppeteer;

/// <summary>
/// 傀儡师机制：双生命池跨战斗保存、本体 / 傀儡形态切换、傀儡形态下的歌单冻结与分组激活。
/// 由实现 <see cref="IPuppeteerCharacter" /> 的角色启用。
/// <para>
/// 依赖演奏机制（<c>perform</c>）：冻结与分组激活都作用在 <c>PerformManager</c> 上，
/// 且战斗开始的顺序必须是"先建好演奏区、再归位形态"。
/// </para>
/// </summary>
public sealed class PuppeteerMechanic : IBangDreamMechanic
{
    public string Id => "puppeteer";

    /// <summary>排在演奏机制（200）之后，保证战斗开始时演奏区已就绪。</summary>
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

    /// <summary>
    /// 本机制不需要 Harmony 补丁：形态切换与死亡拦截全部走原版 <see cref="AbstractModel" /> 钩子
    /// （<c>ShouldDie</c> / <c>AfterPreventingDeath</c> / 战斗结束钩子）。
    /// </summary>
    public void RegisterPatches(ModPatcher patcher)
    {
    }

    public IEnumerable<AbstractModel> InstantiatePlayerState(Player player)
    {
        var manager = (PuppeteerFormManager)ModelDb.RunModel<PuppeteerFormManager>().MutableClone();
        manager.Player = player;
        yield return manager;
    }

    /// <summary>
    /// 死亡拦截必须排在原版防死之前，因此挂到**运行态**订阅组而不是战斗组。
    /// <para>
    /// 原版 <c>Hook.ShouldDie</c> 先遍历全部监听者的 <c>ShouldDie</c>（首个返回 <c>false</c> 者胜出），
    /// 全部通过后才遍历 <c>ShouldDieLate</c>。战斗组订阅者在 <c>CombatState.IterateHookListeners()</c>
    /// 最末产出 —— 排在遗物 / 药水**之后**，于是「精灵药水」（<c>ShouldDie</c> 型防死）会抢先消费，
    /// 本体形态就不会切成傀儡。运行态订阅组排在遗物之前，可保证本体形态的拦截优先。
    /// </para>
    /// <para>
    /// 傀儡形态下本模型返回 <c>true</c>（不拦），原版两轮防死照常生效 —— 墨缇丝可以触发这些机制。
    /// </para>
    /// </summary>
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
