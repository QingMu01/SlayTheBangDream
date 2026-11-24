using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;

namespace BangDreamInSpire.Scripts.Nodes.HandPips;

/// <summary>
/// RitsuLib 副资源的反射桥：不硬引用，没装就整个跳过。
/// 余音（BangDreamInSpire 注册的 LINGERED）这类副资源靠它读 id / 图标 / 当前数量。
/// </summary>
internal static class SecondaryBridge
{
    private const string CmdTypeName = "STS2RitsuLib.Combat.SecondaryResources.SecondaryResourceCmd";
    private const string RegTypeName = "STS2RitsuLib.Combat.SecondaryResources.ModSecondaryResourceRegistry";

    /// <summary>解析失败后多久再试一次：RitsuLib 可能比我们晚加载，不能一次没找到就整局放弃。</summary>
    private const double RetryMs = 2000.0;

    private static MethodInfo _get;
    private static MethodInfo _snapshot;

    // 只在「真的拿到方法」时才置位；否则按 RetryMs 节流重试
    private static double _resolveNextAt;

    public static bool Available
    {
        get
        {
            Resolve();
            return _get != null && _snapshot != null;
        }
    }

    private static void Resolve()
    {
        if (_get != null && _snapshot != null)
        {
            return;
        }

        double now = Godot.Time.GetTicksMsec();
        if (now < _resolveNextAt)
        {
            return;
        }

        _resolveNextAt = now + RetryMs;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type cmd = assembly.GetType(CmdTypeName, false);
            Type reg = assembly.GetType(RegTypeName, false);
            if (cmd == null || reg == null)
            {
                continue;
            }

            MethodInfo get = cmd.GetMethod("Get", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(Player), typeof(string) }, null);
            MethodInfo snapshot = reg.GetMethod("GetDefinitionsSnapshot", BindingFlags.Public | BindingFlags.Static);
            if (get == null || snapshot == null)
            {
                continue; // 类型在、方法还不在：留给下一次重试
            }

            _get = get;
            _snapshot = snapshot;
            return;
        }
    }

    private static MethodInfo _planMethod;
    private static PropertyInfo _planLines;
    private static PropertyInfo _lineId;
    private static PropertyInfo _lineSpend;
    private static PropertyInfo _lineCost;
    private static PropertyInfo _lineFree;
    private static PropertyInfo _lineCanPlay;
    private static double _costNextAt;

    // 反射实参复用（单线程 UI 调用：Tick / 设置页都只在主线程跑）。
    // Plan(card, bool, AbstractModel) 与 Get(player, id) 的参数形状固定，不必每次 new object[]。
    private static readonly object[] PlanArgs = { null, false, null };
    private static readonly object[] GetArgs = { null, null };

    /// <summary>
    /// 这张牌实际要花多少点该副资源。走 RitsuLib 的支付计划（Plan），
    /// 所以遗物/能力带来的「首次休止免费」「消耗全部」这类修正都算进去了。
    /// </summary>
    public static int Cost(MegaCrit.Sts2.Core.Models.CardModel card, string id)
    {
        if (card == null || string.IsNullOrEmpty(id))
        {
            return 0;
        }

        ResolveCost();
        if (_planMethod == null)
        {
            return 0;
        }

        try
        {
            PlanArgs[0] = card;
            object plan = _planMethod.Invoke(null, PlanArgs);
            if (plan == null || _planLines == null)
            {
                return 0;
            }

            if (!(_planLines.GetValue(plan) is System.Collections.IEnumerable lines))
            {
                return 0;
            }

            foreach (object line in lines)
            {
                if (line == null || _lineId == null)
                {
                    continue;
                }

                if (!string.Equals(_lineId.GetValue(line) as string, id, StringComparison.Ordinal))
                {
                    continue;
                }

                bool free = _lineFree != null && (bool)(_lineFree.GetValue(line) ?? false);
                if (free)
                {
                    return 0;
                }

                int spend = _lineSpend != null ? Convert.ToInt32(_lineSpend.GetValue(line) ?? 0) : 0;
                if (spend > 0)
                {
                    return spend;
                }

                int cost = _lineCost != null ? Convert.ToInt32(_lineCost.GetValue(line) ?? 0) : 0;
                return Math.Max(0, cost);
            }
        }
        catch
        {
            return 0;
        }

        return 0;
    }

    /// <summary>支付计划里有没有这一行、以及是否付得起（免单/遗物减免算付得起）。</summary>
    public static bool TryAffordable(MegaCrit.Sts2.Core.Models.CardModel card, string id,
        out bool hasLine, out bool affordable)
    {
        hasLine = false;
        affordable = false;
        if (card == null || string.IsNullOrEmpty(id))
        {
            return false;
        }

        ResolveCost();
        if (_planMethod == null)
        {
            return false;
        }

        try
        {
            PlanArgs[0] = card;
            object plan = _planMethod.Invoke(null, PlanArgs);
            if (plan == null || _planLines == null || !(_planLines.GetValue(plan) is System.Collections.IEnumerable lines))
            {
                return false;
            }

            foreach (object line in lines)
            {
                if (line == null || _lineId == null)
                {
                    continue;
                }

                if (!string.Equals(_lineId.GetValue(line) as string, id, StringComparison.Ordinal))
                {
                    continue;
                }

                hasLine = true;
                bool free = _lineFree != null && (bool)(_lineFree.GetValue(line) ?? false);
                bool canPlay = _lineCanPlay != null && (bool)(_lineCanPlay.GetValue(line) ?? false);
                affordable = free || canPlay;
                return true;
            }
        }
        catch
        {
            return false;
        }

        return hasLine;
    }

    private static void ResolveCost()
    {
        if (_planMethod != null && _planLines != null)
        {
            return;
        }

        double now = Godot.Time.GetTicksMsec();
        if (now < _costNextAt)
        {
            return;
        }

        _costNextAt = now + RetryMs;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type resolver = assembly.GetType("STS2RitsuLib.Combat.SecondaryResources.SecondaryResourcePaymentResolver", false);
            Type plan = assembly.GetType("STS2RitsuLib.Combat.SecondaryResources.SecondaryResourcePaymentPlan", false);
            Type line = assembly.GetType("STS2RitsuLib.Combat.SecondaryResources.SecondaryResourcePaymentLine", false);
            if (resolver == null || plan == null || line == null)
            {
                continue;
            }

            MethodInfo planMethod = resolver.GetMethod("Plan", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(MegaCrit.Sts2.Core.Models.CardModel), typeof(bool), typeof(MegaCrit.Sts2.Core.Models.AbstractModel) },
                null);
            PropertyInfo planLines = plan.GetProperty("Lines");
            if (planMethod == null || planLines == null)
            {
                continue; // 关键成员还没出来：不置位，2 s 后重试
            }

            _planMethod = planMethod;
            _planLines = planLines;
            _lineId = line.GetProperty("ResourceId");
            _lineSpend = line.GetProperty("AmountToSpend");
            _lineCost = line.GetProperty("Cost");
            _lineFree = line.GetProperty("IsFree");
            _lineCanPlay = line.GetProperty("CanPlay");
            return;
        }
    }

    /// <summary>
    /// 定义类型的 PropertyInfo 按 Type 缓存：Snapshot 每 0.25 s 跑一次，
    /// 每次都给每个定义做 3 次 GetProperty（内部还要走一遍反射元数据表）太浪费。
    /// </summary>
    private static readonly Dictionary<Type, PropertyInfo[]> PropCache = new();

    private static PropertyInfo[] PropertiesOf(Type type)
    {
        if (PropCache.TryGetValue(type, out PropertyInfo[] cached))
        {
            return cached;
        }

        var props = new[]
        {
            type.GetProperty("Id"),
            type.GetProperty("SmallIconPath"),
            type.GetProperty("LargeIconPath"),
        };

        if (PropCache.Count > 32)
        {
            PropCache.Clear(); // 上限保护：副资源定义类型本来就少
        }

        PropCache[type] = props;
        return props;
    }

    /// <summary>便利重载：自己 new 一个 List 返回（一次性调用用；每帧/Tick 里请用下面写缓冲的那个）。</summary>
    public static List<OrbRenderer.SecondaryEntry> Snapshot(Player player)
    {
        var result = new List<OrbRenderer.SecondaryEntry>();
        Snapshot(player, result);
        return result;
    }

    /// <summary>把结果写进传入的 List（内部先 Clear，不 new），给每 0.25 s 一次的 Tick 复用。</summary>
    public static void Snapshot(Player player, List<OrbRenderer.SecondaryEntry> into)
    {
        into.Clear();
        if (player == null || !Available)
        {
            return;
        }

        try
        {
            if (!(_snapshot.Invoke(null, null) is Array defs))
            {
                return;
            }

            foreach (object def in defs)
            {
                if (def == null)
                {
                    continue;
                }

                PropertyInfo[] props = PropertiesOf(def.GetType());
                string id = props[0]?.GetValue(def) as string;
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                string icon = props[1]?.GetValue(def) as string
                              ?? props[2]?.GetValue(def) as string;
                GetArgs[0] = player;
                GetArgs[1] = id;
                int amount = Convert.ToInt32(_get.Invoke(null, GetArgs) ?? 0);
                into.Add(new OrbRenderer.SecondaryEntry(id, icon, amount));
            }
        }
        catch (Exception e)
        {
            GD.PushWarning($"[BangDreamInSpire/handpips] secondary: {e.Message}");
        }
    }
}
