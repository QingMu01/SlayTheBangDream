using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamInSpire.Scripts.Nodes.GroupSelect;

/// <summary>
/// 恐惧模式：背景 <c>AnimatedBg</c> 在 <c>SubViewportContainer/SubViewport</c> 里，
/// 不是根节点的直接子节点 —— 基线按「背景的下一个兄弟」插覆盖层会解析失败、落到末尾，
/// 把确认 / 返回整个盖住（表现为「恐惧模式下确认取消不可见」）。
/// <para>
/// 处理：覆盖层改为明确插到 <c>SubViewportContainer</c> 之后 —— 背景之上、所有本体 chrome 之下。
/// 另外恐惧模式不使用进阶选项（固定 10 级、玩家不可选），<see cref="SupportsAscension" /> 为 false，
/// 自定义选择器的进阶面板会随之隐藏。
/// </para>
/// </summary>
internal sealed class DreadSelectAdapter : CharacterSelectAdapter
{
    private const string BackgroundHostPath = "SubViewportContainer";

    /// <summary>
    /// 恐惧屏的「开始轮换连胜」勾选项：<c>RotatingTickbox/Container/RotatingExplanation</c>。
    /// 它位于选择器面板页脚之上，本体配色是近白色，在浅色纸面上看不清。
    /// </summary>
    private const string RotationLabelPath = "RotatingTickbox/Container/RotatingExplanation";

    /// <summary>选择器纸面正文的墨色（与场景里的标题 / 数值文字一致）。</summary>
    private static readonly Color PanelInk = new(0.086f, 0.125f, 0.18f);

    internal static readonly DreadSelectAdapter Instance = new();

    internal override string ModeName => "恐惧";

    internal override bool SupportsAscension => false;

    internal override void InsertOverlay(Node screen, Control overlay)
    {
        screen.AddChildSafely(overlay);

        if (screen.GetNodeOrNull<Control>(BackgroundHostPath) is not { } backgroundHost)
        {
            BangDreamInSpireCore.Logger.Warn(
                "[GroupSelect] NDreadScreen 上找不到 SubViewportContainer，覆盖层将留在末尾（可能遮挡确认 / 返回）。");
            return;
        }

        screen.MoveChild(overlay, backgroundHost.GetIndex() + 1);
    }

    internal override void OnViewChanged(Node screen, bool groupView)
    {
        // 轮换选项属于本体 UI，分组视图时叠在自定义面板上：改成墨色保证可读，
        // 回到本体角色详情视图时撤掉覆盖，恢复本体在深色背景上的白色。
        if (screen.GetNodeOrNull<Control>(RotationLabelPath) is not { } rotationLabel)
        {
            return;
        }

        if (groupView)
        {
            rotationLabel.AddThemeColorOverride("default_color", PanelInk);
        }
        else
        {
            rotationLabel.RemoveThemeColorOverride("default_color");
        }
    }
}
