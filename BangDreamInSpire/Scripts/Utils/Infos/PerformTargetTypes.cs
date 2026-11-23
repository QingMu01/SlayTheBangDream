using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Combat.CardTargeting;

namespace BangDreamInSpire.Scripts.Utils.Infos;

public enum PerformSlotSelectionMode
{
    /// <summary>不进行手动选择，按卡牌自身的入队策略处理。</summary>
    None,

    /// <summary>指定任意已激活槽位（可跨分组）；该槽位原有卡牌离开歌单。</summary>
    Slot,

    /// <summary>指定一个和弦分组；组内按卡牌的常规入队规则落位。</summary>
    Group
}

/// <summary>注册谓词恒假的群体目标类型，以沿用无需生物目标的出牌语义，并由演奏机制接管落位选择。</summary>
/// <remarks>词干派生目标枚举值，发布后改名会破坏存档与联机一致性。</remarks>
public static class PerformTargetTypes
{
    private const string AnySlotStem = "any_slot";

    private const string RequestGroupStem = "request_group";

    private static TargetType? _anySlot;
    private static TargetType? _requestGroup;

    public static TargetType AnySlot => _anySlot ??= Register(AnySlotStem);

    public static TargetType RequestGroup => _requestGroup ??= Register(RequestGroupStem);

    /// <summary>在内容注册阶段确定枚举值；可重复调用。</summary>
    public static void EnsureRegistered()
    {
        _ = AnySlot;
        _ = RequestGroup;
    }

    public static PerformSlotSelectionMode ResolveMode(TargetType target)
    {
        if (target == AnySlot) return PerformSlotSelectionMode.Slot;
        return target == RequestGroup ? PerformSlotSelectionMode.Group : PerformSlotSelectionMode.None;
    }

    public static bool IsManualSlotTarget(TargetType target)
    {
        return ResolveMode(target) != PerformSlotSelectionMode.None;
    }

    private static TargetType Register(string stem)
    {
        return CustomTargetType.RegisterMultiTargetType(BangDreamConst.ModId, stem, static _ => false);
    }
}
