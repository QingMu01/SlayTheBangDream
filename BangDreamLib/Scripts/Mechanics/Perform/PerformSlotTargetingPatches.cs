using System.Reflection;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils.Infos;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace BangDreamLib.Scripts.Mechanics.Perform;

/// <summary>
/// 手动落位目标类型（<see cref="PerformTargetTypes.AnySlot" /> /
/// <see cref="PerformTargetTypes.RequestGroup" />）的出牌交互补丁。
/// </summary>
/// <remarks>
/// 这两个目标类型在原版流程中是"无需生物目标"的群体目标类型，出牌交互按以下方式接入：
/// <list type="bullet">
/// <item><c>NMouseCardPlay.TargetSelection</c>（主入口）：进入槽位选择态，并复用原版目标指示箭头
/// （<see cref="PerformTargetingArrow" />）——卡牌归位到屏幕下方中央、隐藏系统指针、箭头从卡牌指向鼠标，
/// 指向候选槽位时高亮；松手时按指向给出落位请求；</item>
/// <item><c>NMouseCardPlay.IsCardInPlayZone</c> / <c>get_PlayZoneThreshold</c>：原版要求松手时鼠标
/// 位于屏幕上方 75% 的"出牌区"，而角色旁的槽位可能低于该阈值，故对这两种卡放宽；</item>
/// <item><c>NCardPlay.TryPlayCard</c> / <c>NCardPlay.Cleanup</c>：读取选择结果、结束选择态；</item>
/// <item><c>NHandCardHolder.BeginDrag</c>：兜底入口——即使目标选择钩子未生效，也能在选择态下按常规流程出牌。</item>
/// </list>
/// 注意：<c>BeginDrag</c>、<c>IsCardInPlayZone</c> 这类极小方法可能被 JIT 内联，补丁不保证在所有调用点生效，
/// 因此真正的选择交互不依赖它们。
/// </remarks>
public sealed class PerformSlotTargetingPatches : IModPatches
{
    private static readonly FieldInfo IsTryingToPlayCardField =
        AccessTools.Field(typeof(NCardPlay), "_isTryingToPlayCard")!;

    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<BeginManualSlotSelectionPatch>();
        patcher.RegisterPatch<ManualSlotTargetSelectionPatch>();
        patcher.RegisterPatch<ManualSlotPlayZonePatch>();
        patcher.RegisterPatch<ManualSlotPlayZoneThresholdPatch>();
        patcher.RegisterPatch<ManualSlotTryPlayCardPatch>();
        patcher.RegisterPatch<ManualSlotCleanupPatch>();
    }

    /// <summary>取本次出牌涉及的卡牌；未开始出牌时为 <see langword="null" />。</summary>
    internal static CardModel? GetPlayedCard(NCardPlay cardPlay)
    {
        var holder = cardPlay.Holder;
        return holder.CardModel;
    }

    /// <summary>本次出牌的卡牌是否使用手动落位目标类型。</summary>
    internal static bool IsManualSlotCard(NCardPlay cardPlay)
    {
        var card = GetPlayedCard(cardPlay);
        return card != null && PerformTargetTypes.IsManualSlotTarget(card.TargetType);
    }

    /// <summary>原版指示箭头的起点：拖拽中的卡牌节点，缺省时退回卡牌持有节点。</summary>
    internal static Control GetArrowOrigin(NCardPlay cardPlay)
    {
        return cardPlay.Holder.CardNode ?? (Control)cardPlay.Holder;
    }

    /// <summary>
    /// 置位原版"正在尝试出牌"标记：随后原版因"鼠标不在出牌区"发起的取消会失效，
    /// 使松手位置在出牌区下方（角色旁的槽位）时仍能完成出牌。
    /// </summary>
    internal static void AllowPlayOutsidePlayZone(NCardPlay cardPlay)
    {
        IsTryingToPlayCardField.SetValue(cardPlay, true);
    }
}

/// <summary>
/// 兜底：开始拖拽时就进入选择态。主入口是 <see cref="ManualSlotTargetSelectionPatch" />，
/// 两者重复调用是幂等的（<c>BeginSlotSelection</c> 会先结束上一次选择）。
/// </summary>
internal sealed class BeginManualSlotSelectionPatch : IPatchMethod
{
    public static string PatchId => "begin_manual_slot_selection";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NHandCardHolder), nameof(NHandCardHolder.BeginDrag))];
    }

    public static void Postfix(NHandCardHolder __instance)
    {
        var card = __instance.CardModel;
        if (card == null || !PerformTargetTypes.IsManualSlotTarget(card.TargetType)) return;

        var manager = card.Owner.AttachedData().PerformManager;
        manager.BeginManualSlotSelection(card);
        if (manager.IsManualSlotSelectionActive(card))
        {
            PerformTargetingArrow.Begin(__instance.CardNode ?? (Control)__instance);
        }
    }
}

/// <summary>
/// 接管鼠标出牌的目标选择阶段。进入选择态后沿用原版群体目标循环；结束时按松手位置写入落位请求，
/// 或取消本次出牌。未进入选择态（容量为 0、手柄方向导航、指定分组但只有一个可达分组等）时不拦截，
/// 交回原版流程。
/// </summary>
internal sealed class ManualSlotTargetSelectionPatch : IPatchMethod
{
    /// <summary>原版"卡牌归位到屏幕下方中央"（与指向生物时一致）。</summary>
    private static readonly Action<NCardPlay> CenterCard =
        AccessTools.MethodDelegate<Action<NCardPlay>>(
            AccessTools.DeclaredMethod(typeof(NCardPlay), "CenterCard")!);

    /// <summary>原版"是否拖到底部取消区"。</summary>
    private static readonly Func<NMouseCardPlay, bool> IsCardInCancelZone =
        AccessTools.MethodDelegate<Func<NMouseCardPlay, bool>>(
            AccessTools.DeclaredMethod(typeof(NMouseCardPlay), "IsCardInCancelZone")!);

    /// <summary>原版记录左键是否按下的字段。</summary>
    private static readonly AccessTools.FieldRef<NMouseCardPlay, bool> IsLeftMouseDown =
        AccessTools.FieldRefAccess<NMouseCardPlay, bool>("_isLeftMouseDown");

    /// <summary>本次等待的收尾方式。</summary>
    private enum TargetWaitResult
    {
        /// <summary>命中候选槽位：写入落位请求并继续出牌。</summary>
        Selected,

        /// <summary>点空 / 拖入取消区 / 选择态被外部结束：取消本次出牌。</summary>
        Cancelled
    }

    public static string PatchId => "manual_slot_target_selection";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NMouseCardPlay), "TargetSelection", [typeof(TargetMode)])];
    }

    public static bool Prefix(NMouseCardPlay __instance, TargetMode targetMode, ref Task __result)
    {
        var card = PerformSlotTargetingPatches.GetPlayedCard(__instance);
        if (card == null || !PerformTargetTypes.IsManualSlotTarget(card.TargetType)) return true;

        var manager = card.Owner.AttachedData().PerformManager;
        manager.BeginManualSlotSelection(card);
        if (!manager.IsManualSlotSelectionActive(card)) return true;

        BangDreamLibCore.Logger.Info($"Manual slot targeting engaged for {card.Id.Entry} ({targetMode}).");
        PerformTargetingArrow.Begin(PerformSlotTargetingPatches.GetArrowOrigin(__instance));
        __result = RunTargeting(__instance, targetMode, card, manager);
        return false;
    }

    private static async Task RunTargeting(
        NMouseCardPlay instance,
        TargetMode targetMode,
        CardModel card,
        PerformManager manager)
    {
        var result = TargetWaitResult.Cancelled;
        try
        {
            // 与指向生物的目标选择一致：卡牌归位到屏幕下方中央、隐藏系统指针，
            // 由指示箭头从卡牌指向鼠标，而不是让卡牌跟着鼠标走。
            CenterCard(instance);
            result = await WaitForTargetSelection(instance, targetMode, card, manager);
        }
        finally
        {
            if (GodotObject.IsInstanceValid(instance))
            {
                // 松手/点击瞬间再按当前鼠标位置判定一次，避免吃到上一帧的指向。
                manager.RefreshManualSlotSelection(card);
                var wasActive = manager.IsManualSlotSelectionActive(card);
                BangDreamLibCore.Logger.Info(
                    $"Manual slot targeting finished for {card.Id.Entry}: result={result} active={wasActive} " +
                    manager.DescribeManualSlotSelection(card));

                if (result == TargetWaitResult.Cancelled && wasActive)
                {
                    // 与原版单体目标一致：点空 / 拖到底部取消区 → 取消本次出牌。
                    manager.EndManualSlotSelection(card);
                    instance.CancelPlayCard();
                }
                else if (wasActive && manager.TryConsumeManualSlotSelection(card, out _))
                {
                    PerformSlotTargetingPatches.AllowPlayOutsidePlayZone(instance);
                }
                else if (wasActive)
                {
                    // 兜底：选择态仍在却取不到落位时按常规入队规则出牌。
                    BangDreamLibCore.Logger.Warn(
                        $"Manual slot selection yielded no candidate slot for {card.Id.Entry}; " +
                        "falling back to normal enqueue rules.");
                    manager.EndManualSlotSelection(card);
                }
            }
        }
    }

    /// <summary>
    /// 逐条复刻原版鼠标单体目标选择的结束判定（<c>NTargetManager._Input</c>）：
    /// <list type="bullet">
    /// <item>拖拽模式（<see cref="TargetMode.ReleaseMouseToTarget" />）：松开左键时命中候选槽位即选定；
    /// 未命中则与原版一样转入点击模式继续等待，而不是结束；</item>
    /// <item>点击模式（<see cref="TargetMode.ClickMouseToTarget" />）：松开左键时命中候选槽位即选定，
    /// 点空则取消本次出牌；</item>
    /// <item>拖到底部取消区、右键/取消键导致选择态结束、节点失效都提前以取消收尾。</item>
    /// </list>
    /// </summary>
    private static async Task<TargetWaitResult> WaitForTargetSelection(
        NMouseCardPlay instance,
        TargetMode targetMode,
        CardModel card,
        PerformManager manager)
    {
        var mode = targetMode;
        // 拖拽模式下原版 Start() 时左键按下（_isLeftMouseDown = true）：以此作为初值，
        // 使"按下→已松开"发生在等待循环之前时，第一帧就能按"松开"处理（原版同样如此）。
        var wasLeftMouseDown = mode == TargetMode.ReleaseMouseToTarget;

        while (GodotObject.IsInstanceValid(instance) && manager.IsManualSlotSelectionActive(card))
        {
            var isLeftMouseDown = IsLeftMouseDown(instance);
            var isRelease = wasLeftMouseDown && !isLeftMouseDown;
            wasLeftMouseDown = isLeftMouseDown;

            if (isRelease && manager.HasManualSlotCandidate(card))
            {
                return TargetWaitResult.Selected;
            }

            if (isRelease)
            {
                if (mode == TargetMode.ReleaseMouseToTarget)
                {
                    mode = TargetMode.ClickMouseToTarget;
                    BangDreamLibCore.Logger.Info(
                        $"Manual slot targeting switched to click mode for {card.Id.Entry}.");
                }
                else
                {
                    return TargetWaitResult.Cancelled;
                }
            }

            if (IsCardInCancelZone(instance))
            {
                return TargetWaitResult.Cancelled;
            }

            await instance.AwaitProcessFrame();
        }

        return TargetWaitResult.Cancelled;
    }
}

/// <summary>
/// 手动落位卡牌不再要求鼠标位于原版出牌区（屏幕上方 75%）：角色旁的槽位可能低于该阈值。
/// 真正的"必须指向候选槽位"由 <see cref="ManualSlotTargetSelectionPatch" /> 与
/// <see cref="ManualSlotTryPlayCardPatch" /> 保证。
/// </summary>
internal sealed class ManualSlotPlayZonePatch : IPatchMethod
{
    public static string PatchId => "manual_slot_bypass_play_zone";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NMouseCardPlay), "IsCardInPlayZone", Type.EmptyTypes)];
    }

    public static bool Prefix(NMouseCardPlay __instance, ref bool __result)
    {
        if (!PerformSlotTargetingPatches.IsManualSlotCard(__instance)) return true;

        __result = true;
        return false;
    }
}

/// <summary>
/// 与 <see cref="ManualSlotPlayZonePatch" /> 同义，但改在阈值来源处放宽：即使
/// <c>IsCardInPlayZone</c> 被 JIT 内联到调用点，判定仍会读取本属性。
/// </summary>
internal sealed class ManualSlotPlayZoneThresholdPatch : IPatchMethod
{
    public static string PatchId => "manual_slot_bypass_play_zone_threshold";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NMouseCardPlay), "get_PlayZoneThreshold", Type.EmptyTypes)];
    }

    public static bool Prefix(NMouseCardPlay __instance, ref float __result)
    {
        if (!PerformSlotTargetingPatches.IsManualSlotCard(__instance)) return true;

        __result = float.MaxValue;
        return false;
    }
}

/// <summary>
/// 提交出牌时读取手动选择结果：命中候选槽位则放行原方法（本类型无需生物目标，原版以空目标出牌）；
/// 仍在选择态却未命中时取消本次出牌。未提供选择时不拦截，按卡牌自身规则入队。
/// </summary>
internal sealed class ManualSlotTryPlayCardPatch : IPatchMethod
{
    public static string PatchId => "manual_slot_try_play_card";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCardPlay), "TryPlayCard", [typeof(Creature)])];
    }

    public static bool Prefix(NCardPlay __instance)
    {
        if (!PerformSlotTargetingPatches.IsManualSlotCard(__instance)) return true;

        var card = PerformSlotTargetingPatches.GetPlayedCard(__instance)!;
        var manager = card.Owner.AttachedData().PerformManager;
        if (!manager.IsManualSlotSelectionActive(card)) return true;
        if (manager.TryConsumeManualSlotSelection(card, out _)) return true;

        // 仍在选择态却没有取到落位：按卡片自身规则出牌，不取消本次出牌。
        BangDreamLibCore.Logger.Warn(
            $"Manual slot selection yielded no candidate slot for {card.Id.Entry} at play time; " +
            "falling back to normal enqueue rules.");
        manager.EndManualSlotSelection(card);
        return true;
    }
}

/// <summary>
/// 出牌流程收尾：结束选择态与槽位高亮；出牌被取消时一并清空尚未消费的落位请求。
/// </summary>
internal sealed class ManualSlotCleanupPatch : IPatchMethod
{
    public static string PatchId => "end_manual_slot_selection";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCardPlay), "Cleanup")];
    }

    public static void Prefix(NCardPlay __instance, bool isFinished)
    {
        if (!PerformSlotTargetingPatches.IsManualSlotCard(__instance)) return;

        var card = PerformSlotTargetingPatches.GetPlayedCard(__instance)!;
        var manager = card.Owner.AttachedData().PerformManager;
        if (isFinished)
        {
            manager.EndManualSlotSelection(card);
            return;
        }

        manager.CancelManualSlotSelection(card);
    }
}
