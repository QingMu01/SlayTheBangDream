using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Mechanics;

/// <summary>
/// 机制模块的注册与装配中心：负责发现/登记机制、按依赖拓扑排序，并向核心提供装配所需的聚合视图。
/// </summary>
public static class BangDreamMechanicRegistry
{
    private static readonly List<IBangDreamMechanic> Registered = [];
    private static IReadOnlyList<IBangDreamMechanic>? _ordered;

    /// <summary>已按依赖顺序排好的机制列表。</summary>
    public static IReadOnlyList<IBangDreamMechanic> Mechanics => _ordered ??= OrderByDependencies(Registered);

    /// <summary>
    /// 扫描程序集中所有 <see cref="IBangDreamMechanic" /> 实现并登记。
    /// </summary>
    /// <remarks>
    /// 单个机制类型出问题不应让整条装配链只留下一条指向反射的栈：
    /// 类型加载失败逐条记日志后继续登记其余类型；构造失败则带上类型名与内层异常原因抛出，
    /// 缺少无参构造时直接给出可读的说明（<see cref="Activator.CreateInstance(Type, bool)" /> 只会报
    /// <see cref="MissingMethodException" />）。
    /// </remarks>
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
    /// <remarks>
    /// 装配**不**按 <see cref="IBangDreamMechanic.RequiredCharacterCapabilities" /> 过滤：该成员是声明性元数据，
    /// 能力判定由各机制在自己的 manager 内部早退（例如 <see cref="Lingered.LingeredOrbitManager" /> 在
    /// <c>SubmitCombatState</c> 里用 <c>BangDreamCapabilities</c> 判定后直接返回）。
    /// 因此未实现对应能力接口的角色同样会拿到状态模型实例，只是在 manager 里保持空转。
    /// </remarks>
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

    /// <summary>
    /// 全部机制需要在**运行态**被订阅的模型。这些模型在 <c>RunState.IterateHookListeners</c> 中
    /// 排在遗物 / 药水之前，供「必须优先于原版判定」的钩子使用。
    /// </summary>
    public static IEnumerable<AbstractModel> GetRunStateHookModels(IEnumerable<Player> players)
    {
        var playerList = players.ToList();
        return Mechanics.SelectMany(mechanic => playerList.SelectMany(mechanic.GetRunStateHookModels));
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
