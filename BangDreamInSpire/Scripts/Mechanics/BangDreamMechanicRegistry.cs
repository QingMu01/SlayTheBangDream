using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Mechanics;

/// <summary>发现、排序并装配玩法机制。</summary>
public static class BangDreamMechanicRegistry
{
    private static readonly List<IBangDreamMechanic> Registered = [];
    private static IReadOnlyList<IBangDreamMechanic>? _ordered;

    /// <summary>按依赖排序的机制。</summary>
    public static IReadOnlyList<IBangDreamMechanic> Mechanics => _ordered ??= OrderByDependencies(Registered);

    /// <summary>扫描并登记机制；类型加载失败跳过，构造失败带类型信息抛出。</summary>
    public static void Discover(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            foreach (var loaderException in ex.LoaderExceptions.OfType<Exception>())
            {
                BangDreamInSpireCore.Logger.Error(
                    $"Failed to load a type from {assembly.GetName().Name}: {loaderException}");
            }

            BangDreamInSpireCore.Logger.Error(
                $"Type discovery for {assembly.GetName().Name} continued with " +
                $"{ex.Types.OfType<Type>().Count()} of {ex.Types.Length} loadable types.");
            types = ex.Types.OfType<Type>().ToArray();
        }

        foreach (var type in types)
        {
            if (!type.IsClass || type.IsAbstract || !typeof(IBangDreamMechanic).IsAssignableFrom(type))
            {
                continue;
            }

            var constructor = type.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
            if (constructor == null)
            {
                throw new InvalidOperationException(
                    $"机制类型 '{type.FullName}' 没有无参构造，无法被自动登记；" +
                    "请补一个无参构造，或改用 BangDreamMechanicRegistry.Register(...) 手动登记实例。");
            }

            IBangDreamMechanic mechanic;
            try
            {
                mechanic = (IBangDreamMechanic)constructor.Invoke(null);
            }
            catch (TargetInvocationException ex)
            {
                throw new InvalidOperationException(
                    $"机制类型 '{type.FullName}' 构造失败：{ex.InnerException?.Message}",
                    ex.InnerException ?? ex);
            }

            Register(mechanic);
        }
    }

    /// <summary>登记机制，ID 重复时抛出异常。</summary>
    public static void Register(IBangDreamMechanic mechanic)
    {
        if (Registered.Any(existing => string.Equals(existing.Id, mechanic.Id, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"重复的机制 id：'{mechanic.Id}'。");
        }

        Registered.Add(mechanic);
        _ordered = null;
    }

    /// <summary>按类型去重创建玩家模型；不按能力过滤，能力早退由机制内部处理。</summary>
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

    /// <summary>返回战斗订阅模型，按机制与玩家排序。</summary>
    public static IEnumerable<AbstractModel> GetCombatHookModels(IEnumerable<Player> players)
    {
        var playerList = players.ToList();
        return Mechanics.SelectMany(mechanic => playerList.SelectMany(mechanic.GetCombatHookModels));
    }

    /// <summary>返回运行态订阅模型，在遗物与药水前执行。</summary>
    public static IEnumerable<AbstractModel> GetRunStateHookModels(IEnumerable<Player> players)
    {
        var playerList = players.ToList();
        return Mechanics.SelectMany(mechanic => playerList.SelectMany(mechanic.GetRunStateHookModels));
    }

    /// <summary>通知战斗开始。</summary>
    public static void SubmitCombatState(Player player)
    {
        foreach (var mechanic in Mechanics)
        {
            mechanic.SubmitCombatState(player);
        }
    }

    /// <summary>通知战斗结束清理。</summary>
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
