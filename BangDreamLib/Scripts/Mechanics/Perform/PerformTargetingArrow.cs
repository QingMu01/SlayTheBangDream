using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace BangDreamLib.Scripts.Mechanics.Perform;

/// <summary>
/// 手动落位选择期间复用原版的目标指示箭头——与指向生物时是同一套表现：
/// 箭头自拖拽中的卡牌出发跟随鼠标、隐藏系统指针，指向候选槽位时高亮，结束时恢复指针。
/// </summary>
/// <remarks>
/// 原版箭头由 <see cref="NTargetManager" /> 私有持有的 <c>TargetingArrow</c> 子节点提供，
/// 其目标选择会话要求"单体目标类型"（本机制的类型是群体类型，会抛异常），
/// 因此这里只驱动箭头本身，不进入原版的目标选择会话。
/// </remarks>
internal static class PerformTargetingArrow
{
    private static readonly FieldInfo? ArrowField = AccessTools.Field(typeof(NTargetManager), "_targetingArrow");

    private static bool _isDrawing;

    private static NTargetingArrow? Arrow
    {
        get
        {
            var manager = NRun.Instance?.GlobalUi?.TargetManager;
            if (manager == null) return null;

            return ArrowField?.GetValue(manager) as NTargetingArrow ??
                   manager.GetNodeOrNull<NTargetingArrow>("TargetingArrow");
        }
    }

    /// <summary>自指定控件开始绘制箭头（鼠标模式：箭头跟随鼠标）。手柄方向导航下不绘制。</summary>
    public static void Begin(Control from)
    {
        if (NControllerManager.Instance?.IsUsingDirectionalNavigation == true) return;

        var arrow = Arrow;
        if (arrow == null) return;

        arrow.StartDrawingFrom(from, usingController: false);
        _isDrawing = true;
    }

    /// <summary>指向候选槽位时高亮（沿用原版友方目标的配色与放大反馈）。</summary>
    public static void SetHighlighted(bool highlighted)
    {
        if (!_isDrawing) return;

        var arrow = Arrow;
        if (arrow == null) return;

        if (highlighted)
        {
            arrow.SetHighlightingOn(isEnemy: false);
        }
        else
        {
            arrow.SetHighlightingOff();
        }
    }

    /// <summary>结束绘制并恢复鼠标指针。</summary>
    public static void End()
    {
        if (!_isDrawing) return;

        _isDrawing = false;
        Arrow?.StopDrawing();
    }
}
