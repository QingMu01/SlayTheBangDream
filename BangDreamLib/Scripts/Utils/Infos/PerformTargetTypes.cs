using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Combat.CardTargeting;

namespace BangDreamLib.Scripts.Utils.Infos;

/// <summary>
/// 音乐牌出牌时的手动落位选择模式。
/// </summary>
public enum PerformSlotSelectionMode
{
    /// <summary>不进行手动选择，按卡牌自身的入队策略处理。</summary>
    None,

    /// <summary>指定任意已激活槽位（可跨分组）；该槽位原有卡牌离开歌单。</summary>
    Slot,

    /// <summary>指定一个和弦分组；组内按卡牌的常规入队规则落位。</summary>
    Group
}

/// <summary>
/// 手动指定落位的目标类型。取值经 RitsuLib 的 <see cref="CustomTargetType" /> 以模组命名空间
/// 确定性生成，并注册为**群体目标类型**（谓词恒为假）：
/// <list type="bullet">
/// <item>原版与 RitsuLib 的所有目标判定（<c>IsValidTarget(null)</c>、<c>CanPlayTargeting(null)</c>、
/// 群体目标指示器）都按"不需要生物目标"处理，因此出牌不会被目标校验拦住；</item>
/// <item>生物指示器不显示任何候选，槽位/分组的选择由演奏机制自行接管
/// （见 <c>PerformSlotTargetingPatches</c>）。</item>
/// </list>
/// </summary>
/// <remarks>
/// 词干一旦发布即冻结：枚举数值由 <c>模组ID_TARGETTYPE_词干</c> 派生，改名会破坏存档与联机一致性。
/// </remarks>
public static class PerformTargetTypes
{
    /// <summary><see cref="AnySlot" /> 的模组内词干。</summary>
    private const string AnySlotStem = "any_slot";

    /// <summary><see cref="RequestGroup" /> 的模组内词干。</summary>
    private const string RequestGroupStem = "request_group";

    private static TargetType? _anySlot;
    private static TargetType? _requestGroup;

    /// <summary>指定任意已激活槽位，并替换该槽位中已有的卡牌。</summary>
    public static TargetType AnySlot => _anySlot ??= Register(AnySlotStem);

    /// <summary>放入指定分组，其他规则不变。</summary>
    public static TargetType RequestGroup => _requestGroup ??= Register(RequestGroupStem);

    /// <summary>
    /// 在内容注册阶段显式完成注册，使枚举值与其他注册内容同时机确定。幂等。
    /// </summary>
    public static void EnsureRegistered()
    {
        _ = AnySlot;
        _ = RequestGroup;
    }

    /// <summary>取目标类型对应的手动选择模式；非手动落位类型返回 <see cref="PerformSlotSelectionMode.None" />。</summary>
    public static PerformSlotSelectionMode ResolveMode(TargetType target)
    {
        if (target == AnySlot) return PerformSlotSelectionMode.Slot;
        return target == RequestGroup ? PerformSlotSelectionMode.Group : PerformSlotSelectionMode.None;
    }

    /// <summary>目标类型是否需要玩家手动指定落位。</summary>
    public static bool IsManualSlotTarget(TargetType target)
    {
        return ResolveMode(target) != PerformSlotSelectionMode.None;
    }

    private static TargetType Register(string stem)
    {
        // 群体目标类型 + 恒假谓词：不选任何生物，只借用"无需生物目标"的出牌语义。
        return CustomTargetType.RegisterMultiTargetType(BangDreamConst.ModId, stem, static _ => false);
    }
}
