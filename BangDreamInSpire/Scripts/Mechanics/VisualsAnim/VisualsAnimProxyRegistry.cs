using MegaCrit.Sts2.Core.Entities.Creatures;

namespace BangDreamInSpire.Scripts.Mechanics.VisualsAnim;

/// <summary>
/// 演出代理的注册表：生物 → <see cref="VisualsAnimProxy" />。
/// <para>
/// 代理按生物缓存并复用（克隆骨骼是有成本的），演出开始时显示、结束时隐藏；
/// 战斗结束或生物离开时统一 <see cref="Clear" /> 彻底释放。
/// </para>
/// </summary>
public static class VisualsAnimProxyRegistry
{
    private static readonly Dictionary<Creature, VisualsAnimProxy> Proxies =
        new(ReferenceEqualityComparer.Instance);

    private static readonly Lock Gate = new();

    /// <summary>取一个可用的代理并进入演出态；无渲染环境返回 null。</summary>
    public static VisualsAnimProxy? Acquire(Creature creature)
    {
        ArgumentNullException.ThrowIfNull(creature);

        lock (Gate)
        {
            if (Proxies.TryGetValue(creature, out var existing) && existing.IsAlive)
            {
                existing.Begin();
                return existing;
            }

            if (existing != null)
                Proxies.Remove(creature);

            var created = VisualsAnimProxy.TryCreate(creature);
            if (created == null)
                return null;

            Proxies[creature] = created;
            created.Begin();
            return created;
        }
    }

    /// <summary>结束演出态（保留克隆供下次复用）。</summary>
    public static void Release(VisualsAnimProxy? proxy)
    {
        proxy?.End();
    }

    /// <summary>查询正在演出的代理，供 <c>VfxSpawnPosition</c> 重定向使用。</summary>
    public static bool TryGetActive(Creature? creature, out VisualsAnimProxy proxy)
    {
        proxy = null!;
        if (creature == null)
            return false;

        lock (Gate)
        {
            if (Proxies.TryGetValue(creature, out var found) && found.IsActive && found.IsAlive)
            {
                proxy = found;
                return true;
            }
        }

        return false;
    }

    /// <summary>彻底释放全部代理（战斗结束）。</summary>
    public static void Clear()
    {
        VisualsAnimProxy[] all;
        lock (Gate)
        {
            all = Proxies.Values.ToArray();
            Proxies.Clear();
        }

        foreach (var proxy in all)
            proxy.Restore();
    }
}
