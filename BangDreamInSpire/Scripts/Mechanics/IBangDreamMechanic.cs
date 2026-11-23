using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Patching.Core;

namespace BangDreamInSpire.Scripts.Mechanics;

/// <summary>可独立装配的玩法机制模块。</summary>
public interface IBangDreamMechanic
{
    /// <summary>机制唯一标识。</summary>
    string Id { get; }

    /// <summary>装配权重，小者先执行。</summary>
    int Order => 0;

    /// <summary>依赖的机制 ID，缺失或成环时装配失败。</summary>
    IReadOnlyList<string> Dependencies => [];

    /// <summary>声明能力元数据；不过滤装配，机制内部自行早退。</summary>
    IReadOnlyList<Type> RequiredCharacterCapabilities => [];

    /// <summary>注册静态内容。</summary>
    void RegisterContent(BangDreamMechanicContext context);

    /// <summary>注册 Harmony 补丁。</summary>
    void RegisterPatches(ModPatcher patcher);

    /// <summary>创建并绑定玩家状态模型。</summary>
    IEnumerable<AbstractModel> InstantiatePlayerState(Player player) => [];

    /// <summary>返回战斗 hook 模型，在遗物与药水后执行。</summary>
    IEnumerable<AbstractModel> GetCombatHookModels(Player player) => [];

    /// <summary>返回运行态 hook 模型，在遗物与药水前执行，供需优先判定的钩子使用。</summary>
    IEnumerable<AbstractModel> GetRunStateHookModels(Player player) => [];

    /// <summary>战斗开始回调。</summary>
    void SubmitCombatState(Player player)
    {
    }

    /// <summary>战斗结束清理。</summary>
    void UnsubscribeCombatState(Player player)
    {
    }
}
