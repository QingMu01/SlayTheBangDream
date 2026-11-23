using MegaCrit.Sts2.Core.Entities.Creatures;

namespace BangDreamInSpire.Scripts.Mechanics.VisualsAnim;

/// <summary>按生物缓存演出代理，战斗结束时统一释放。</summary>
public static class VisualsAnimProxyRegistry
{
    private static readonly Dictionary<Creature, VisualsAnimProxy> Proxies =
        new(ReferenceEqualityComparer.Instance);

    private static readonly Lock Gate = new();

    /// <summary>获取代理并开始演出，无渲染时返回 null。</summary>
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

    /// <summary>结束演出并保留代理。</summary>
    public static void Release(VisualsAnimProxy? proxy)
    {
        proxy?.End();
    }

    /// <summary>查询演出中的代理。</summary>
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

    /// <summary>释放全部代理。</summary>
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
