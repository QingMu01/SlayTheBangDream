using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Patching.Core;

namespace BangDreamInSpire.Scripts.Mechanics;

/// <summary>
/// 一个可独立增删的玩法机制模块。模块自行声明它需要的内容注册、补丁、玩家状态与战斗生命周期钩子，
/// 由 <see cref="BangDreamMechanicRegistry" /> 统一装配，核心初始化流程不再感知具体机制。
/// </summary>
public interface IBangDreamMechanic
{
    /// <summary>机制唯一标识，用作补丁分组名与去重键。建议使用小写下划线形式，例如 <c>extra_deck</c>。</summary>
    string Id { get; }

    /// <summary>装配排序权重，数值小者先装配。依赖机制的权重应小于依赖方。</summary>
    int Order => 0;

    /// <summary>本机制所依赖的其它机制 <see cref="Id" />；缺失或成环会在装配阶段抛出异常。</summary>
    IReadOnlyList<string> Dependencies => [];

    /// <summary>启用本机制所需的角色能力接口类型；为空表示机制全局装配（不依赖特定角色）。</summary>
    IReadOnlyList<Type> RequiredCharacterCapabilities => [];

    /// <summary>注册关键字、牌堆、二级资源、奖励、节点附加、Run 数据等静态内容。</summary>
    void RegisterContent(BangDreamMechanicContext context);

    /// <summary>向核心提供的补丁分组注册本机制的 Harmony 补丁。全量注入，时机与核心一致。</summary>
    void RegisterPatches(ModPatcher patcher);

    /// <summary>创建附着于玩家的状态模型实例（通常为单例模型的克隆），并按需完成玩家绑定。</summary>
    IEnumerable<AbstractModel> InstantiatePlayerState(Player player) => [];

    /// <summary>需要在战斗期间被订阅进原版战斗 hook 迭代的模型（<c>ExtraSubscribe</c>）。</summary>
    /// <remarks>
    /// 这组模型在 <c>CombatState.IterateHookListeners()</c> 的**最末**产出，
    /// 因此**排在遗物 / 药水之后**。纯玩法钩子放这里即可。
    /// </remarks>
    IEnumerable<AbstractModel> GetCombatHookModels(Player player) => [];

    /// <summary>
    /// 需要被订阅进**运行态** hook 迭代的模型。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="GetCombatHookModels" /> 的区别只在迭代位置：run-state 订阅者在
    /// <c>RunState.IterateHookListeners</c> 中产出，**排在牌组之前的空档、遗物 / 药水之前**。
    /// 因此「必须优先于原版同类判定生效」的钩子要放这里 ——
    /// 例如傀儡师的 <c>ShouldDie</c>：原版对 <c>ShouldDie</c> 的迭代是首个返回 <c>false</c> 者胜出，
    /// 若排在遗物之后，「精灵药水」这类 <c>ShouldDie</c> 型防死会抢先消费掉，本体就不会切成傀儡。
    /// </remarks>
    IEnumerable<AbstractModel> GetRunStateHookModels(Player player) => [];

    /// <summary>战斗开始时的每玩家订阅。</summary>
    void SubmitCombatState(Player player)
    {
    }

    /// <summary>战斗结束时的每玩家清理。</summary>
    void UnsubscribeCombatState(Player player)
    {
    }
}
