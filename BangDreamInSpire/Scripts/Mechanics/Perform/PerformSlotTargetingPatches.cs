using System.Reflection;
using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Utils.Infos;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace BangDreamInSpire.Scripts.Mechanics.Perform;

/// <summary>手动槽位与分组目标补丁。</summary>
/// <remarks>0.113.0 不透传非生物 nodeFilter，需自建选择流程；选择态放宽出牌区。</remarks>
public sealed class PerformSlotTargetingPatches : IModPatches
{
    /// <summary>反射定位本体字段，失败返回 null 并由调用点降级，
    /// 避免静态初始化异常绕过补丁保护。</summary>
    private static readonly FieldInfo? IsTryingToPlayCardField =
        AccessTools.Field(typeof(NCardPlay), "_isTryingToPlayCard");

    private static int _degradationLogged;

    /// <summary>签名变动导致的降级只告警一次。</summary>
    internal static void LogMissingMemberOnce(string members)
    {
        if (Interlocked.Exchange(ref _degradationLogged, 1) == 1)
            return;

        BangDreamInSpireCore.Logger.Warn(
            $"本体私有成员 {members} 定位失败：手动落位目标选择的相关能力已降级，" +
            "出牌仍按原版规则进行。本体升级后请复核该成员是否改名。");
    }

    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<BeginManualSlotSelectionPatch>();
        patcher.RegisterPatch<ManualSlotTargetSelectionPatch>();
        patcher.RegisterPatch<ManualSlotPlayZonePatch>();
        patcher.RegisterPatch<ManualSlotPlayZoneThresholdPatch>();
        patcher.RegisterPatch<ManualSlotTryPlayCardPatch>();
        patcher.RegisterPatch<ManualSlotCleanupPatch>();
    }

    internal static CardModel? GetPlayedCard(NCardPlay cardPlay)
    {
        var holder = cardPlay.Holder;
        return holder.CardModel;
    }

    internal static bool IsManualSlotCard(NCardPlay cardPlay)
    {
        var card = GetPlayedCard(cardPlay);
        return card != null && InsertSongRules.IsManualSlotCard(card);
    }

    internal static bool IsSelectionActive(NCardPlay cardPlay)
    {
        var card = GetPlayedCard(cardPlay);
        return card != null && card.Owner.AttachedData().PerformManager.IsManualSlotSelectionActive(card);
    }

    /// <summary>箭头起点优先取拖拽卡牌，缺省时取持有节点。</summary>
    internal static Control GetArrowOrigin(NCardPlay cardPlay)
    {
        return cardPlay.Holder.CardNode ?? (Control)cardPlay.Holder;
    }

    /// <summary>标记正在出牌，避免低于原版出牌区的槽位被取消。</summary>
    internal static void AllowPlayOutsidePlayZone(NCardPlay cardPlay)
    {
        if (IsTryingToPlayCardField == null)
        {
            LogMissingMemberOnce("NCardPlay._isTryingToPlayCard");
            return;
        }

        IsTryingToPlayCardField.SetValue(cardPlay, true);
    }
}

/// <summary>拖拽开始时兜底进入选择；与主入口重复调用幂等。</summary>
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
        if (card == null || !InsertSongRules.IsManualSlotCard(card)) return;

        var manager = card.Owner.AttachedData().PerformManager;
        manager.BeginManualSlotSelection(card);
        if (manager.IsManualSlotSelectionActive(card))
        {
            PerformTargetingArrow.Begin(__instance.CardNode ?? (Control)__instance);
        }
    }
}

/// <summary>驱动槽位选择并写入落位结果；无需选择时放行原版流程。</summary>
internal sealed class ManualSlotTargetSelectionPatch : IPatchMethod
{

    /// <summary>原版卡牌归位方法，缺失仅影响表现。</summary>
    private static readonly Action<NCardPlay>? CenterCard =
        CreateDelegate<Action<NCardPlay>>(typeof(NCardPlay), "CenterCard");

    /// <summary>底部取消区判定，缺失时禁用该取消方式。</summary>
    private static readonly Func<NMouseCardPlay, bool>? IsCardInCancelZone =
        CreateDelegate<Func<NMouseCardPlay, bool>>(typeof(NMouseCardPlay), "IsCardInCancelZone");

    /// <summary>左键状态字段，缺失时退回原版。</summary>
    private static readonly AccessTools.FieldRef<NMouseCardPlay, bool>? IsLeftMouseDown =
        CreateFieldAccessor<NMouseCardPlay, bool>("_isLeftMouseDown");

    private static TDelegate? CreateDelegate<TDelegate>(Type declaringType, string methodName)
        where TDelegate : Delegate
    {
        try
        {
            var method = AccessTools.DeclaredMethod(declaringType, methodName);
            return method == null ? null : AccessTools.MethodDelegate<TDelegate>(method);
        }
        catch (Exception exception)
        {
            BangDreamInSpireCore.Logger.Warn(
                $"本体私有方法 {declaringType.Name}.{methodName} 定位失败，手动落位选择将降级：{exception.Message}");
            return null;
        }
    }

    private static AccessTools.FieldRef<TDeclaring, TField>? CreateFieldAccessor<TDeclaring, TField>(string fieldName)
        where TDeclaring : class
    {
        try
        {
            return AccessTools.FieldRefAccess<TDeclaring, TField>(fieldName);
        }
        catch (Exception exception)
        {
            BangDreamInSpireCore.Logger.Warn(
                $"本体私有字段 {typeof(TDeclaring).Name}.{fieldName} 定位失败，手动落位选择将降级：{exception.Message}");
            return null;
        }
    }

    private enum TargetWaitResult
    {
        Selected,

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
        if (card == null || !InsertSongRules.IsManualSlotCard(card)) return true;

        var manager = card.Owner.AttachedData().PerformManager;
        manager.BeginManualSlotSelection(card);
        if (!manager.IsManualSlotSelectionActive(card)) return true;

        BangDreamInSpireCore.Logger.Info($"Manual slot targeting engaged for {card.Id.Entry} ({targetMode}).");
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
            // 归位卡牌并用箭头指示；CenterCard 缺失仅跳过归位。
            if (CenterCard != null)
                CenterCard(instance);
            result = await WaitForTargetSelection(instance, targetMode, card, manager);
        }
        finally
        {
            if (GodotObject.IsInstanceValid(instance))
            {
                // 松手时刷新指向，避免使用上一帧结果。
                manager.RefreshManualSlotSelection(card);
                var wasActive = manager.IsManualSlotSelectionActive(card);
                BangDreamInSpireCore.Logger.Info(
                    $"Manual slot targeting finished for {card.Id.Entry}: result={result} active={wasActive} " +
                    manager.DescribeManualSlotSelection(card));

                if (result == TargetWaitResult.Cancelled && wasActive)
                {
                    // 点空或拖入取消区时取消。
                    manager.EndManualSlotSelection(card);
                    instance.CancelPlayCard();
                }
                else if (wasActive && manager.TryConsumeManualSlotSelection(card, out _))
                {
                    PerformSlotTargetingPatches.AllowPlayOutsidePlayZone(instance);
                }
                else if (wasActive)
                {
                    // 取不到落位时按常规入队。
                    BangDreamInSpireCore.Logger.Warn(
                        $"Manual slot selection yielded no candidate slot for {card.Id.Entry}; " +
                        "falling back to normal enqueue rules.");
                    manager.EndManualSlotSelection(card);
                }
            }
        }
    }

    /// <summary>复刻原版结束判定：拖拽未命中转点击，点击未命中取消；
    /// 取消区、外部结束与节点失效均取消。</summary>
    private static async Task<TargetWaitResult> WaitForTargetSelection(
        NMouseCardPlay instance,
        TargetMode targetMode,
        CardModel card,
        PerformManager manager)
    {
        var isLeftMouseDownAccessor = IsLeftMouseDown;
        var isCancelZoneAccessor = IsCardInCancelZone;
        if (isLeftMouseDownAccessor == null || isCancelZoneAccessor == null)
        {
            // 缺少关键判定时退回原版，避免死循环。
            PerformSlotTargetingPatches.LogMissingMemberOnce(
                "NMouseCardPlay._isLeftMouseDown / NMouseCardPlay.IsCardInCancelZone");
            return TargetWaitResult.Cancelled;
        }

        var mode = targetMode;
        // 以原版拖拽初始按下状态处理首帧松开。
        var wasLeftMouseDown = mode == TargetMode.ReleaseMouseToTarget;

        while (GodotObject.IsInstanceValid(instance) && manager.IsManualSlotSelectionActive(card))
        {
            var isLeftMouseDown = isLeftMouseDownAccessor(instance);
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
                    BangDreamInSpireCore.Logger.Info(
                        $"Manual slot targeting switched to click mode for {card.Id.Entry}.");
                }
                else
                {
                    return TargetWaitResult.Cancelled;
                }
            }

            if (isCancelZoneAccessor(instance))
            {
                return TargetWaitResult.Cancelled;
            }

            await instance.AwaitProcessFrame();
        }

        return TargetWaitResult.Cancelled;
    }
}

/// <summary>选择态放宽原版出牌区，未选择时保持原规则。</summary>
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
        if (!PerformSlotTargetingPatches.IsManualSlotCard(__instance) ||
            !PerformSlotTargetingPatches.IsSelectionActive(__instance)) return true;

        __result = true;
        return false;
    }
}

/// <summary>在阈值来源处放宽，覆盖 IsCardInPlayZone 被内联的情况。</summary>
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
        if (!PerformSlotTargetingPatches.IsManualSlotCard(__instance) ||
            !PerformSlotTargetingPatches.IsSelectionActive(__instance)) return true;

        __result = float.MaxValue;
        return false;
    }
}

/// <summary>命中候选槽位时放行原版空目标出牌；未提供选择时按卡牌规则入队。</summary>
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

        // 未取到落位时按卡牌规则入队。
        BangDreamInSpireCore.Logger.Warn(
            $"Manual slot selection yielded no candidate slot for {card.Id.Entry} at play time; " +
            "falling back to normal enqueue rules.");
        manager.EndManualSlotSelection(card);
        return true;
    }
}

/// <summary>结束选择并在取消时清空未消费的落位请求。</summary>
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
