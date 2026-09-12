using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Patching.Core;

namespace BangDreamLib.Scripts.Mechanics;

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
    IEnumerable<AbstractModel> GetCombatHookModels(Player player) => [];

    /// <summary>战斗开始时的每玩家订阅。</summary>
    void SubmitCombatState(Player player)
    {
    }

    /// <summary>战斗结束时的每玩家清理。</summary>
    void UnsubscribeCombatState(Player player)
    {
    }
}
