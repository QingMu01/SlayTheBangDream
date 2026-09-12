using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Mechanics;

/// <summary>
/// 机制模块的注册与装配中心：负责发现/登记机制、按依赖拓扑排序，并向核心提供装配所需的聚合视图。
/// </summary>
public static class BangDreamMechanicRegistry
{
    private static readonly List<IBangDreamMechanic> Registered = [];
    private static IReadOnlyList<IBangDreamMechanic>? _ordered;

    /// <summary>已按依赖顺序排好的机制列表。</summary>
    public static IReadOnlyList<IBangDreamMechanic> Mechanics => _ordered ??= OrderByDependencies(Registered);

    /// <summary>扫描程序集中所有 <see cref="IBangDreamMechanic" /> 实现并登记。</summary>
    public static void Discover(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract || !typeof(IBangDreamMechanic).IsAssignableFrom(type))
            {
                continue;
            }

            Register((IBangDreamMechanic)Activator.CreateInstance(type, nonPublic: true)!);
        }
    }

    /// <summary>登记单个机制实例；<see cref="IBangDreamMechanic.Id" /> 重复时抛出异常。</summary>
    public static void Register(IBangDreamMechanic mechanic)
    {
        if (Registered.Any(existing => string.Equals(existing.Id, mechanic.Id, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"重复的机制 id：'{mechanic.Id}'。");
        }

        Registered.Add(mechanic);
        _ordered = null;
    }

    /// <summary>全部机制为指定玩家实例化的状态模型，按具体类型去重（重复类型保留最先注册者）。</summary>
    public static IEnumerable<AbstractModel> InstantiatePlayerStates(Player player)
    {
        var seen = new HashSet<Type>();
        foreach (var mechanic in Mechanics)
        {
            foreach (var model in mechanic.InstantiatePlayerState(player))
            {
                if (seen.Add(model.GetType()))
                {
                    yield return model;
                }
            }
        }
    }

    /// <summary>
    /// 全部机制在战斗中需要被订阅的模型。按机制分组、机制内再按玩家顺序展开，
    /// 与各机制自行追加订阅列表的历史顺序保持一致。
    /// </summary>
    public static IEnumerable<AbstractModel> GetCombatHookModels(IEnumerable<Player> players)
    {
        var playerList = players.ToList();
        return Mechanics.SelectMany(mechanic => playerList.SelectMany(mechanic.GetCombatHookModels));
    }

    /// <summary>调用全部机制的战斗开始订阅。</summary>
    public static void SubmitCombatState(Player player)
    {
        foreach (var mechanic in Mechanics)
        {
            mechanic.SubmitCombatState(player);
        }
    }

    /// <summary>调用全部机制的战斗结束清理。</summary>
    public static void UnsubscribeCombatState(Player player)
    {
        foreach (var mechanic in Mechanics)
        {
            mechanic.UnsubscribeCombatState(player);
        }
    }

    private static IReadOnlyList<IBangDreamMechanic> OrderByDependencies(IReadOnlyList<IBangDreamMechanic> mechanics)
    {
        var byId = new Dictionary<string, IBangDreamMechanic>(StringComparer.Ordinal);
        foreach (var mechanic in mechanics)
        {
            byId[mechanic.Id] = mechanic;
        }

        foreach (var mechanic in mechanics)
        {
            foreach (var dependency in mechanic.Dependencies)
            {
                if (!byId.ContainsKey(dependency))
                {
                    throw new InvalidOperationException($"机制 '{mechanic.Id}' 依赖了不存在的机制 '{dependency}'。");
                }
            }
        }

        var ordered = new List<IBangDreamMechanic>();
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        void Visit(IBangDreamMechanic mechanic)
        {
            if (visited.Contains(mechanic.Id))
            {
                return;
            }

            if (!visiting.Add(mechanic.Id))
            {
                throw new InvalidOperationException($"机制依赖存在环：'{mechanic.Id}'。");
            }

            foreach (var dependency in mechanic.Dependencies.OrderBy(dep => byId[dep].Order))
            {
                Visit(byId[dependency]);
            }

            visiting.Remove(mechanic.Id);
            visited.Add(mechanic.Id);
            ordered.Add(mechanic);
        }

        foreach (var mechanic in mechanics.OrderBy(m => m.Order).ThenBy(m => m.Id, StringComparer.Ordinal))
        {
            Visit(mechanic);
        }

        return ordered;
    }
}
