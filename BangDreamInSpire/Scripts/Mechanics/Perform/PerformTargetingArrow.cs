using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace BangDreamInSpire.Scripts.Mechanics.Perform;

/// <summary>手动落位时复用原版箭头，保持与生物目标一致的指向和高亮表现。</summary>
/// <remarks>原版目标选择会话要求单体目标类型，群体类型会抛异常，因此只驱动 <see cref="NTargetManager" /> 的私有箭头，不进入该会话。</remarks>
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

    /// <summary>手柄方向导航下不绘制箭头。</summary>
    public static void Begin(Control from)
    {
        if (NControllerManager.Instance?.IsUsingDirectionalNavigation == true) return;

        var arrow = Arrow;
        if (arrow == null) return;

        arrow.StartDrawingFrom(from, usingController: false);
        _isDrawing = true;
    }

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

    public static void End()
    {
        if (!_isDrawing) return;

        _isDrawing = false;
        Arrow?.StopDrawing();
    }
}
